# Spec-001 — Lot 06A : SQLite et sauvegarde/restauration

Date : 16 octobre 2026, Europe/Paris. Statut : lot 06A terminé et validé dans le périmètre ci-dessous.

## Périmètre

Ce lot conserve les quatre tables historiques Boxes, Areas, Categories et BoxCats, ainsi que le nom BigBazar.db3. Leur simplification reste réservée au lot 06B. Aucune référence personnelle n'a été modifiée ; aucune application installée sur un appareil personnel n'a été désinstallée ou réinitialisée.

## Paquets et fournisseur natif

Le paquet retenu reste celui indiqué par Microsoft : sqlite-net-pcl, auteur SQLite-net, propriétaire praeclarum. Version stable vérifiée et restaurée : 1.11.285.

Graphe réellement résolu sur Android et Windows :

| Paquet | Version |
| --- | --- |
| sqlite-net-pcl | 1.11.285 |
| SQLitePCLRaw.core | 3.0.3 |
| SQLitePCLRaw.provider.e_sqlite3 | 3.0.3 |
| SourceGear.sqlite3 | 3.53.3 |

L'ancienne référence directe SQLitePCLRaw.bundle_green 2.1.10 est retirée de l'application et des tests. Aucun paquet d'accès concurrent n'a été ajouté. Les tests natifs et le démarrage Android Debug fonctionnent sans initialisation manuelle Batteries supplémentaire.

L'audit NuGet du graphe restauré ne signale aucun paquet vulnérable à la date du contrôle. Journal : .local/step06a-nuget-audit.log. Cela constitue un constat daté, pas une garantie pour les audits futurs.

## Accès sérialisés

DatabaseStore gère une connexion, son initialisation, son état et un SemaphoreSlim commun. Toutes les opérations de DataService utilisant cette connexion prennent un accès exclusif. Les appels internes qui prenaient auparavant une autre méthode publique sont remplacés par des requêtes internes ou un helper exécutés sous le même accès, afin d'éviter une attente récursive du verrou.

Les opérations de sauvegarde, remplacement, fermeture, réouverture et réinitialisation utilisent aussi ce verrou. Le mode SQLite FullMutex remplace SharedCache ; les accès applicatifs restent sérialisés. GetAllActiveBoxNumbers bénéficie désormais lui aussi de l'initialisation attendue et du verrou.

Une erreur d'initialisation laisse IsInitialized à false et ferme la connexion concernée ; un nouvel essai peut réussir après réparation.

## Sauvegarde

- Utilisation de SQLiteAsyncConnection.BackupAsync, qui produit un instantané cohérent, y compris lorsque des données validées sont encore dans le WAL.
- La connexion reste ouverte et utilisable. Les sélecteurs ne sont pas affichés avec une connexion fermée.
- Noms uniques horodatés pour ne pas écraser une sauvegarde existante.
- Sur Windows, sélection du dossier sur le thread UI avec Microsoft.Windows.Storage.Pickers.FolderPicker lié à l'AppWindow, puis création et export de l'instantané. Cette API renvoie un chemin sans objet StorageFolder à marshaler.
- Sur Android, instantané dans le cache partageable de l'application, puis ouverture du partage sur le thread UI. Aucune livraison à un destinataire n'est annoncée : l'API de partage ne confirme pas la réception et permet la fermeture du sélecteur.
- Une annulation renvoie false ; une erreur est journalisée et présentée par DialogService. Le sélecteur Windows renvoie null à l'annulation ; ses erreurs sont propagées vers le dialogue d'échec. Après export réussi, un dialogue confirme le chemin du fichier.

Les instantanés Android restent dans le cache pour permettre leur lecture par l'application destinataire après fermeture de la boîte de partage. Les copies de secours avant restauration sont conservées dans AppDataDirectory/DatabaseRecovery. Aucune politique de purge automatique n'a été ajoutée.

## Restauration

1. Sélection sur le thread UI. Une annulation laisse la connexion intacte et renvoie false. Sur Windows, FileOpenPicker du Windows App SDK est lié à l'AppWindow et renvoie un chemin.
2. Lecture par flux : OpenReadAsync sur Android, File.OpenRead du chemin sélectionné sur Windows. Aucune dépendance à FullPath ni à un chemin directement accessible sur Android.
3. Copie dans un fichier temporaire unique du même répertoire que la base active.
4. Vérification de l'intégrité SQLite et des colonnes historiques minimales de Boxes. Un fichier vide ou une base SQLite étrangère est rejeté.
5. Initialisation du schéma sur cette copie, puis lecture des quatre modèles pour détecter aussi des valeurs incompatibles, par exemple une date hors plage. Aucun changement de la base active à ce stade.
6. Sous verrou exclusif, création et contrôle d'une sauvegarde fraîche de la base active dans DatabaseRecovery.
7. Fermeture, remplacement par le fichier préparé, puis ouverture et initialisation attendues.
8. En cas d'échec de remplacement, réouverture de la base initiale ; en cas d'échec après remplacement, restauration depuis la copie de secours et tentative de réouverture. Si cette récupération échoue également, une exception contient les deux erreurs et le chemin du secours.
9. Suppression du fichier temporaire. Le ViewModel n'annonce le redémarrage et ne quitte l'application que si la restauration a réellement réussi.

Les bases BigBazar sans Areas sont acceptées : la table est créée vide, et les colonnes manquantes de Boxes sont ajoutées selon le comportement historique de SQLite-net. Les valeurs des colonnes initiales restent inchangées.

## Tests natifs Windows

21 tests réussis, sans test ignoré, d'abord avec données synthétiques puis avec les nouvelles copies des références :

- Initialisation vide, réexécution, concurrence, échec et nouvel essai après réparation.
- Instantané de données validées dans le WAL ; refus d'écraser la base active ou une sauvegarde existante ; connexion encore utilisable.
- Restauration depuis un flux non repositionnable ; conservation des identifiants, numéros, dates, emplacements, catégories et associations ; copie de secours fraîche.
- Rejet de fichier vide, texte, base étrangère, colonnes manquantes, numéros dupliqués et date invalide.
- Annulation et interruption de lecture ; aucun remplacement ; connexion utilisable et fichier temporaire nettoyé.
- Remplacement refusé sous Windows à cause d'un fichier ouvert sans partage de suppression : base initiale réouverte et écriture possible, secours conservé.
- Exclusion mutuelle des accès, sauvegarde, restauration et réinitialisation.
- Restauration, sauvegarde et seconde restauration des jeux vide/72/71/30 boîtes ; comparaison intégrale des colonnes d'origine des quatre tables et integrity_check = ok.

Copies créées avec scripts/verify_reference_databases.py sous .local/reference-step06a. Les empreintes des sauvegardes d'origine correspondent au manifeste. Les tests recopient ces références dans de nouveaux fichiers temporaires ; aucune donnée personnelle n'est intégrée aux fixtures du dépôt.

Journaux : .local/step06a-tests.log, .local/step06a-tests-reference.log et .local/finish06a-tests-save-feedback.log (21 tests réussis lors du dernier passage).

## Compilation et contrôle des binaires

- Windows Debug et Release non empaquetés : zéro erreur.
- Android Debug autonome, Release normal et Release d'identité isolée : zéro erreur.
- Dernière compilation après correction des sélecteurs Windows : .local/finish06a-windows-desktop-pickers.log (zéro erreur) et .local/finish06a-android-desktop-picker-guard.log (zéro avertissement, zéro erreur).
- La DLL e_sqlite3.dll Windows Release a la même empreinte SHA-256 que celle de SourceGear.sqlite3 3.53.3 pour win-x64 : B7385D722C83FB52142A00477A726723745916D22A555711EE89834C1111FB2E.
- scripts/check_android_native_alignment.py a contrôlé les segments ELF des 256 bibliothèques 64 bits de l'APK Release normal. Aucune anomalie ; les deux libe_sqlite3.so arm64-v8a/x86_64 ont des segments alignés sur 16 384 octets. Les bibliothèques sont compressées dans cet APK et le manifeste utilise extractNativeLibs=true.
- zipalign -c -P 16 4 retourne le code 0 sur ce même APK.
- Ces vérifications statiques sont complétées par une recette sur l'image officielle Android 15 Google APIs 16 KB Page Size x86_64 ; getconf PAGE_SIZE retourne 16384.

Avertissements restants : propriétés MVVM/WinRT sous Windows, langue PRI Windows et variable options inutilisée du rapport. Les collisions d'assemblages de l'ancienne compilation Android isolée sont résolues en conservant les chemins intermédiaires par défaut du SDK.

## Recette Android

Émulateur dédié emulator-5580, Android 15/API 35 x86_64. Les identités sont distinctes de com.enaxos.smallbazar :
- Debug : com.enaxos.smallbazar.sqlitedebug.
- Release : com.enaxos.smallbazar.sqlitevalidation.

Debug :
- Base vide : application visible, compteur 0.
- Annulation du sélecteur de restauration : retour aux outils, aucun message de succès/redémarrage, application utilisable.
- Restauration de trois boîtes synthétiques depuis Downloads par le sélecteur système : réussite ; message de redémarrage ; après redémarrage, compteur 3 et valeurs attendues.
- Une copie de secours de la base vide est présente et intègre.
- Save : ouverture du sélecteur de partage avec un fichier .db3. Aucun destinataire sélectionné. Après fermeture du sélecteur, l'application reste utilisable. Le fichier produit a été relu : trois boîtes, integrity_check = ok.
- Copie de la référence 72 boîtes chargée dans l'identité Debug isolée : compteur 72. Relecture après arrêt et comparaison de toutes les valeurs : Boxes 72, Areas 11, Categories 37, BoxCats 18 ; aucune valeur d'origine modifiée, intégrité correcte.

Release :
- L'ancien crash de lancement est résolu : la commande de validation imposait un IntermediateOutputPath unique aux différentes architectures Android et provoquait des collisions pendant l'édition des assemblages. Le binaire ainsi obtenu n'était pas fiable malgré une compilation sans erreur.
- Un contrôle après recompilation incrémentale a aussi détecté une incohérence de l'assembly store (« Compressed assembly ... is larger than when the application was built »). Le script de validation utilise désormais -t:Rebuild, avec les chemins par défaut, pour reconstruire ensemble les assemblages et leurs tables natives de compression.
- La reconstruction complète réussit (.local/finish06a-android-clean-validation.log). Après redémarrage du PC confirmé par l'utilisateur, le dernier APK est réinstallé et lancé à froid sur emulator-5582 : PAGE_SIZE = 16384, lancement COLD réussi, trois boîtes Alpha/Bravo/Charlie conservées. Save ouvre le partage avec un fichier .db3 unique ; sa fermeture sans destinataire revient aux outils, application toujours active. L'émulateur est ensuite arrêté.
- APK final validé : bin/Release/net10.0-android/com.enaxos.smallbazar.sqlitevalidation-Signed.apk ; SHA-256 : 3E67CAE009CEBA6BAA0A5DF96DE9FCEB7697E3F175F513EB6C9AEBADCEF6D376. Contrôle ELF final : .local/finish06a-alignment-final.json, aucune anomalie ; zipalign -c -P 16 4 retourne 0.
- Compilation avec les chemins par défaut du SDK et uniquement une ApplicationId de validation. AOT et trimming conservent leurs réglages normaux ; aucune racine de préservation spéculative ni désactivation n'est ajoutée au projet. Le script scripts/build_android_validation.ps1 reproduit cette commande avec -m:1.
- Sur emulator-5580 (pages 4 Ko), démarrage à vide, annulation de Restore, restauration synthétique, redémarrage avec trois boîtes et ouverture du partage Save réussis.
- Sur emulator-5582, image officielle Android 15/API 35 Google APIs 16 KB Page Size x86_64 révision 5, Emulator 37.2.12, getconf PAGE_SIZE = 16384 : mêmes parcours réussis. Après restauration et redémarrage, compteur 3 et Alpha/Bravo/Charlie test box visibles. Save ouvre le partage avec un nom .db3 unique ; fermeture du partage sans destinataire, retour aux outils.
- Journaux : .local/finish06a-release-final.log, .local/finish06a-alignment.json et .local/finish06a-tests-reference.log. Les anciens journaux de crash sont conservés comme historique du défaut de commande.

## Recette Windows

L'utilisateur lance la version isolée copiée dans E:\source\SmallBazarSolution\Validation06A. Une base synthétique Sample-3-boxes.db3 est fournie à côté de l'exécutable. Le premier retour signale que Save ne produit aucun fichier ni message. Après ajout d'une gestion explicite des erreurs et d'une confirmation du chemin, l'utilisateur précise que le sélecteur apparaît, mais que sa validation bloque Save.

La capture dotnet-stack/dotnet-dump du processus de validation situe précisément l'attente : Task<Windows.Storage.StorageFolder> retient FolderPickerImplementation.InternalPickAsync, DataService.BackupDatabaseAsync et la commande MVVM. Un thread reste dans IAsyncOperationMethods.GetResults. Aucun appel de sauvegarde SQLite n'est encore engagé. Fichiers locaux : .local/finish06a-windows-hang-stack.txt et .local/finish06a-windows-hang-async.txt.

Correction : sélecteurs de bureau Microsoft.Windows.Storage.Pickers du Windows App SDK 1.8 déjà présent, liés à l'AppWindow et renvoyant des chemins. Le flux de restauration Windows est ouvert directement avec File.OpenRead ; Android conserve OpenReadAsync. Compilation Windows Release corrigée : zéro erreur, journal .local/finish06a-windows-desktop-pickers.log. Après remplacement de la copie de validation, l'utilisateur confirme que la manipulation passe (Save avec confirmation, puis ouverture et annulation de Restore).

L'utilisateur a également trouvé un processus de validation encore actif après fermeture de la fenêtre et l'a terminé. Ce processus verrouillait les DLL et empêchait la copie de mise à jour. Le blocage natif constaté dans le sélecteur constitue une piste pour cette persistance, sans établir à lui seul la cause de la fermeture incomplète ; le comportement doit être revérifié après correction.

## Limites restant ouvertes

La recette Windows des sélecteurs est confirmée par l'utilisateur. Le runtime Computer Use échoue encore à l'initialisation (« permission path cannot be represented losslessly »), ce qui empêche une recette automatisée de ces dialogues. Les tests natifs Windows couvrent la couche SQLite et ses opérations de fichiers.

L'utilisateur signale aussi des raccourcis TextAid semblant s'activer comme si Ctrl restait appuyé. Sa capture montre ensuite un Explorateur bloqué sur « Travail en cours… », même dans Accueil ; certains lancements depuis la barre des tâches, dont WhatsApp, échouent sans message. Aucun lien causal avec SmallBazar n'est établi et aucune commande clavier Windows n'a été envoyée pendant ce diagnostic. L'arrêt de l'émulateur (environ 4 Go libérés) et le redémarrage d'Explorer n'ont pas suffi. Après redémarrage complet du PC, l'utilisateur confirme que Windows fonctionne mieux et que Save fonctionne bien. Le contrôle final Android reprend seulement après ce retour.

Le démarrage Android Release et les parcours SQLite sur émulateur 16 Ko sont vérifiés, y compris le dernier APK entièrement reconstruit. Les essais sur appareil physique, les mises à jour en place, la signature et la publication restent à traiter dans leurs lots respectifs. Le lot 06A est terminé ; le lot 06B n'est pas engagé dans ce travail.

## Commandes reproductibles

Depuis la racine du projet :

```powershell
dotnet restore SmallBazar.csproj --disable-parallel
dotnet test tests/SmallBazar.Data.Tests/SmallBazar.Data.Tests.csproj -m:1
$env:SMALLBAZAR_REFERENCE_DIRECTORY = (Resolve-Path .local/reference-step06a).Path
dotnet test tests/SmallBazar.Data.Tests/SmallBazar.Data.Tests.csproj -m:1 --no-restore
Remove-Item Env:SMALLBAZAR_REFERENCE_DIRECTORY
dotnet build SmallBazar.csproj -m:1 -f net10.0-android -c Release --no-restore
dotnet build SmallBazar.csproj -m:1 -f net10.0-windows10.0.19041.0 -c Release --no-restore -p:WindowsPackageType=None -p:AppxPackageSigningEnabled=false -p:GenerateAppInstallerFile=false -p:ApplicationId=com.enaxos.smallbazar.validation -p:IntermediateOutputPath=obj/windows-sqlite-release/ -p:OutputPath=.local/windows-sqlite-release/
dotnet list SmallBazar.csproj package --vulnerable --include-transitive --no-restore
python scripts/check_android_native_alignment.py bin/Release/net10.0-android/com.enaxos.smallbazar-Signed.apk
```

Utiliser le Python disponible sur le poste si python n'est pas dans PATH. Pour Android isolé, exécuter scripts/build_android_validation.ps1 (ou -Configuration Debug) : le script ajoute ApplicationId et, en Debug, EmbedAssembliesIntoApk=true. Conserver les répertoires intermédiaires par défaut du SDK pour préserver leur séparation par architecture. Toutes les actions ADB sélectionnent explicitement emulator-5580 ou emulator-5582.

## Sources officielles

- [Microsoft MAUI 10 : paquet SQLite](https://learn.microsoft.com/en-us/dotnet/maui/data-cloud/database-sqlite?view=net-maui-10.0).
- [sqlite-net-pcl 1.11.285 et dépendances](https://www.nuget.org/packages/sqlite-net-pcl/1.11.285).
- [SourceGear.sqlite3 3.53.3](https://www.nuget.org/packages/SourceGear.sqlite3/3.53.3).
- [API de sauvegarde SQLite](https://sqlite.org/backup.html).
- [Android : compatibilité avec les pages de 16 Ko](https://developer.android.com/guide/practices/page-sizes).
- [Microsoft : sélecteurs de fichiers et dossiers du Windows App SDK](https://learn.microsoft.com/windows/apps/develop/files/using-file-folder-pickers).
