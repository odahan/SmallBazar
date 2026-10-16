# Spec-001 — Étape 08 : adaptations Android

Date : 16 octobre 2026, Europe/Paris. Statut : étape terminée avec exception de signature historique explicitement autorisée. Recettes API 21/35/36 et smartphone API 33 effectuées ; 72 boîtes et 11 emplacements Windows restaurés et comparés intégralement. Correction du thème sombre livrée sur le téléphone en version 1.5.8/code 3.

## Adaptations

Le manifeste déclare la visibilité de l'intention `android.intent.action.SENDTO` avec le schéma `mailto`, nécessaire à la détection du client mail par MAUI sur Android récent.

Le style commun des `ContentPage` utilise `SafeAreaEdges=All` sur Android pour respecter les barres système, les encoches et le clavier. `App.xaml` configure le mode Android `WindowSoftInputModeAdjust=Resize` : sans ce mode, l'essai a montré que le clavier recouvrait encore les boutons du détail. Avec les deux réglages, l'éditeur et les boutons restent au-dessus du clavier. Les couleurs et le padding des pages restent hérités de `BasePageStyle`. Les autres plateformes conservent `SafeAreaEdges=None`.

Les lignes du titre, de l'emplacement et des boutons du détail passent en hauteur automatique sur Android. En paysage sur l'écran de validation 640 × 320, leurs anciennes hauteurs fixes réduisaient l'éditeur à deux pixels ; le réglage final lui laisse 65 pixels et affiche son contenu. Les hauteurs Windows restent inchangées.

La recette complémentaire sur Android 5.0.2/API 21 révèle un crash natif au lancement : `A TaskDescription's primary color should be opaque`. La ressource Android `colorPrimary` utilisait `#80512BD4`, avec un canal alpha non opaque. Elle devient `#FF512BD4`, en conservant ses composantes RVB. Le minimum API 21 reste en place. Journal du défaut avant correction : `.local/step08-api21-before-fix-logcat.txt`.

`allowBackup=true` est conservé. Deux jeux de règles excluent uniquement le fichier des préférences chiffrées MAUI SecureStorage : `fullBackupContent` pour Android 11 et antérieurs, `dataExtractionRules` pour le cloud et le transfert entre appareils sur Android 12 et suivants. Aucun `include` restrictif n'est ajouté : la base et les autres fichiers métier restent éligibles à la sauvegarde système. Le cache est exclu par Android. Les sauvegardes manuelles SQLite restent inchangées.

Les règles sont générées depuis `Platforms/Android/BackupRules/*.xml.in` dans le répertoire intermédiaire de chaque cible, avant le calcul des ressources Android. Le nom du fichier exclu utilise l'`ApplicationId` réellement compilé. Cette substitution explicite évite de conserver un marqueur `${applicationId}` littéral dans une ressource XML et couvre aussi les identités de validation. Les fichiers générés sont enregistrés dans `FileWrites` pour le nettoyage MSBuild.

Une mise à jour sur le même appareil conserve le stockage sécurisé existant ; une restauration sur un autre appareil doit demander à nouveau la clé, puisque la clé de chiffrement Android Keystore n'est pas transférée. Aucun changement du code de connexion ou de stockage OpenAI n'est introduit.

## Chaîne technique et compatibilité

| Élément | Valeur contrôlée |
| --- | --- |
| SDK .NET et workload set | 10.0.401, fixés dans `global.json` |
| Pack Android Windows | 36.1.69 |
| Plateforme de compilation et cible manifeste | Android 16 / API 36, plateforme révision 2 |
| Build Tools | 36.0.0 |
| JDK Android | Microsoft OpenJDK 21.0.8+9-LTS |
| Minimum manifeste | API 21, inchangé |
| Émulateur de recette | `emulator-5582`, Android 15 / API 35 x86_64, pages de 16 384 octets |

La compilation n'impose pas de relèvement du minimum. Aucune exclusion supplémentaire d'appareils n'est décidée. La poursuite de la recette couvre aussi Android 5.0.2/API 21 et Android 16/API 36, avec les résultats détaillés ci-dessous. Les contrôles du smartphone sont distingués de ceux des émulateurs.

## Identité, version et signature de distribution

Le smartphone est connecté pendant la poursuite de la recette, après le premier inventaire sans appareil. Le relevé en lecture seule révèle une identité Android différente de celle supposée à partir de Windows :

| Élément de l'installation du smartphone | Valeur |
| --- | --- |
| Android | 13 / API 33 |
| ApplicationId installé | `com.companyname.smallbazar` |
| Version affichée dans le paquet | 1.5.7 |
| Code de version | 1 |
| API minimale / cible | 21 / 34 |
| Certificat | `CN=Android Debug, O=Android, C=US` |
| Empreinte SHA-256 | `9abb713f16040c97019a811606cab7c11c29b8b58220617d9a03cced903ca616` |

Le projet fixe donc `ApplicationId=com.companyname.smallbazar` uniquement dans le groupe Android ; Windows conserve `com.enaxos.smallbazar`. Android utilise le nom de version 1.5.8 : code 2 pour la première réinstallation, puis code 3 pour la correction du thème sombre, supérieurs au code 1 relevé. L'affichage historique forcé à 1.6.4 par `VersionService` reste à remplacer et à harmoniser à l'étape 09 ; le relevé et la comparaison portent sur les métadonnées Android réelles.

Les quatre APK de l'installation (base, arm64, langue et densité) sont copiés dans `E:\Backups\SmallBazar\20261016T2008286368917Z\Android-installed`, avec certificat, métadonnées et manifeste SHA-256. Le relevé initial est en lecture seule ; l'utilisateur autorise ensuite une réinstallation avec restauration, l'ancienne clé étant irrécupérable.

Les deux magasins `debug.keystore` retrouvés dans le profil local Xamarin et sa vue virtualisée Codex portent tous deux le certificat actuel `31a906…0773f3c`, créé le 16 octobre 2026. Il ne correspond pas au certificat de l'APK de 2024. La recherche de noms de magasins dans le profil utilisateur, `E:\source` et `E:\Backups` n'a pas retrouvé l'ancienne clé. L'utilisateur confirme qu'elle ne peut pas être retrouvée et autorise la réinstallation après contrôle des données, plutôt qu'une mise à jour en place impossible avec ce certificat.

Cette exception à la conservation de la signature historique est explicitement autorisée pour ce téléphone. L'utilisateur confirme également que la connexion OpenAI sera modifiée ultérieurement et que la perte de sa clé locale lors de la réinstallation n'est pas bloquante. Le nouveau magasin de signature est sauvegardé, après autorisation explicite de cette copie privée hors Git, dans `New-Android-signing/debug.keystore` sous le dossier de sauvegarde ci-dessus. Le nouvel APK est conservé à côté. Les prochaines mises à jour pourront utiliser ce même nouveau certificat ; aucune clé privée n'est enregistrée dans le dépôt.

Le premier lot de recette utilise `com.enaxos.smallbazar.sqlitevalidation` sur API 35. Les essais complémentaires API 36 et smartphone utilisent ensuite l’identité Android réelle, comme détaillé plus bas. L'APK et la base synthétique déjà présents sont sauvegardés avant mise à jour dans `.local/step08-before-update.apk` et `.local/step08-before-update.db3`. L'APK précédent a le code 1 ; le nouveau code 2 est passé uniquement en argument de compilation. Le script de validation accepte désormais `-ApplicationVersion`, avec 3 comme valeur par défaut ; le projet fixe également le code Android de distribution à 3.

Le certificat de validation est `CN=Android Debug, O=Android, C=US`, empreinte SHA-256 `31a9067770d340c953eecd86f7c5efa6201b3d886e6460f705c7a33cc0773f3c`. Ce certificat ne prouve pas la compatibilité de mise à jour de l'application personnelle.

## Commandes reproductibles

```powershell
./scripts/build_android_validation.ps1 -Configuration Release -ApplicationVersion 2

dotnet build SmallBazar.csproj -m:1 -t:Rebuild -f net10.0-android -c Release -p:ApplicationId=com.enaxos.smallbazar.sqlitevalidation -p:ApplicationVersion=2

python scripts/check_android_native_alignment.py bin/Release/net10.0-android/com.enaxos.smallbazar.sqlitevalidation-Signed.apk

adb -s emulator-5582 install -r bin/Release/net10.0-android/com.enaxos.smallbazar.sqlitevalidation-Signed.apk
```

La compilation Release conserve AOT et trimming, les chemins intermédiaires du SDK et la séparation par architecture. Adapter les chemins des outils au poste ; le script Python fonctionne aussi avec le Python fourni par Codex. Le code 2 est un numéro de test : le choisir au moins égal à celui de l'identité de validation déjà installée lors d'une prochaine recette.

## Résultats du premier lot de compilation et binaires de validation

- Android Release du premier lot : réussite, zéro erreur, un avertissement `CS0219` préexistant dans le rapport. Journal `.local/step08-build-android-release-final.log`.
- Windows Release non empaqueté, après les dernières modifications XAML : réussite, zéro erreur, 35 avertissements préexistants MVVM/WinRT et `CS0219`. Journal `.local/step08-build-windows-release.log`.
- Manifeste fusionné : `minSdkVersion=21`, `targetSdkVersion=36`, intention `SENDTO/mailto`, `allowBackup=true` et références aux deux jeux de règles présents.
- Règles binaires de l'APK relues avec `aapt2 dump xmltree` : nom réel de l'identité de validation, sans marqueur non remplacé. Génération isolée pour l'identité de distribution également vérifiée dans `obj/step08-production-rules/backup-rules`.
- APK historique du premier lot (remplacé par le binaire de distribution décrit en clôture) : `bin/Release/net10.0-android/com.enaxos.smallbazar.sqlitevalidation-Signed.apk`, SHA-256 `06FA6949D4E2637B054A30E1A89B98FC4915829C450999858D477151D99FCA1D`.
- Signature finale vérifiée avec `apksigner` : certificat identique à l'APK de validation précédent. Résultat `.local/step08-final-signature.txt`.
- Contrôle ELF des 230 bibliothèques 64 bits, dont SQLite arm64/x86_64 : aucune anomalie ; `.local/step08-native-alignment.json`. `zipalign -c -P 16 4` retourne 0.

## Recette sur émulateur

Pour ce premier lot, seule l'identité de validation sur `emulator-5582` est installée ou modifiée. L'émulateur démarre sans fenêtre, avec 2 Go de RAM, puis est arrêté après la recette. Les réglages de rotation et du gestionnaire de sauvegarde sont rétablis avant l'arrêt.

| Parcours | Résultat et preuve locale |
| --- | --- |
| Mise à jour en place | `adb install -r` : succès du code 1 vers 2, même certificat, date de première installation conservée. Démarrage à froid réussi, trois boîtes visibles. `.local/step08-after-update.xml` et `.png`. |
| Conservation des données | Comparaison SQL avant/après : toutes les valeurs de `Boxes` et `Areas` identiques ; trois boîtes et deux emplacements, `integrity_check=ok`, migration du schéma 0 vers 1. Les copies de recette restent dans `.local`. |
| Barres système et clavier | Liste et détail dans les zones visibles. Clavier du détail ouvert : éditeur, emplacement et boutons accessibles. Retour ferme le clavier. `.local/step08-final-detail-keyboard.xml` et `.png`. |
| Rotation | Portrait → paysage → portrait : boîte sélectionnée conservée, texte et boutons visibles. `.local/step08-final-landscape.xml` et `.png`. |
| Modale Areas | Ouverture avec deux emplacements. Retour reste neutralisé par le code existant ; Close revient aux outils. `.local/step08-areas-modal.xml`, `step08-areas-back.xml`, `step08-areas-closed.xml`. |
| Fournisseur de documents | Restore → confirmation → Downloads → `step08-legacy-3-boxes.db3` : succès, dialogue de redémarrage ; trois boîtes visibles après redémarrage. `.local/step08-downloads.xml`, `step08-restore-result.xml`, `step08-restored-list.xml`. |
| Partage de sauvegarde | Save ouvre le partage système avec un fichier `.db3` unique ; Retour l'annule et revient aux outils. `.local/step08-database-share.xml` et `step08-share-cancelled.xml`. |
| Rapport hors ligne | Cache synthétique de trois boîtes injecté uniquement dans l'identité de test ; rapport affiché et HTML relu avec les trois descriptions et emplacements attendus. Export ouvre le partage `SharedReport.html` ; Retour revient au rapport. `.local/step08-final-report.png`, `step08-final-report-loaded.xml`, `step08-final-html-share.xml` et `step08-SharedReport.html`. Aucun Refresh ni appel OpenAI. |
| Client mail | Résolution `SENDTO/mailto` vers Gmail, puis lancement de son accueil de configuration. Aucun compte ajouté, aucun message envoyé. `.local/step08-mailto-client.xml`. La commande mail historique n'est pas exposée dans les vues actuelles ; le lancement par `Email.ComposeAsync` depuis l'application n'est donc pas recetté. |

## Sauvegarde système Android

Le transport `com.android.localtransport/.LocalTransport` est sélectionné temporairement, sans compte ni upload cloud. Deux fichiers de préférences synthétiques sont préparés : un fichier ordinaire et un fichier portant exactement le nom SecureStorage. Ces marqueurs ne sont pas des clés chiffrées.

Un premier essai depuis un paquet en état force-stop est refusé par Android. Après lancement de l'application et retour à l'accueil, `bmgr backupnow com.enaxos.smallbazar.sqlitevalidation` réussit. L'archive locale est copiée et inspectée :

- `f/BigBazar.db3` présent ;
- `sp/step08-backup-probe.xml` présent ;
- préférences SecureStorage absentes ;
- cache de l'application, fichier de rapport partagé et cache de catégorisation absents.

Preuve : `.local/step08-auto-backup-check.json`. La sauvegarde peut inclure des sous-répertoires WebView nommés `cache` hors du répertoire cache de l'application ; ils ne sont pas confondus avec celui-ci dans la vérification. Les deux marqueurs synthétiques sont ensuite retirés. Le transport initial est rétabli et le gestionnaire, initialement désactivé, est de nouveau désactivé.

Ce test valide le filtrage effectif d'une sauvegarde locale sur API 35. Le cloud Google, le transfert réel entre appareils, les anciennes règles Android ≤ 11 et la conservation d'une vraie clé SecureStorage après mise à jour ne sont pas validés par cet essai.

## Limites avant distribution

La mise à jour avec le certificat historique n'est pas réalisable ; l'utilisateur autorise explicitement une réinstallation avec la nouvelle signature après validation des sauvegardes. Le stockage de clé OpenAI sera reconfiguré ultérieurement à sa demande, sans test distant ni récupération de secret dans ce lot. La réinstallation, la restauration et la mise à jour suivante sont confirmées sur le smartphone dans la section de clôture ci-dessous. Le cloud Google et le transfert réel entre appareils ne font pas partie des essais effectués ; le filtrage des préférences est vérifié avec le transport local Android. La conservation de la vraie clé OpenAI historique ne peut pas être validée après la réinstallation autorisée.

## Complément de recette API 21 et API 36

Les images officielles `system-images;android-21;default;x86_64` révision 5 et `system-images;android-36;default;x86_64` révision 2 sont installées dans le SDK local de recette. Un seul émulateur de recette reste actif à la fois.

- API 21 : Emulator 37.2.12 reste bloqué avant son démarrage ; l'émulateur 35.1.20 déjà installé démarre l'image correctement. Le premier lancement révèle le crash de couleur native opaque décrit plus haut. Après correction, démarrage à vide puis chargement et migration des trois boîtes synthétiques réussis. Clavier et paysage gardent le détail et les boutons visibles. Comparaison SQL intégrale de `Boxes` et `Areas` réussie, intégrité correcte, schéma 1. L'image ancienne n'a pas de stockage SD écrivable ; les captures utilisent `/data/local/tmp`. Le paquet de validation au nom long produit aussi un avertissement de chargement AOT d'une bibliothèque dont le chemin dépasse la limite du linker Android 5 ; le processus continue et les parcours testés réussissent. Aucun trimming/AOT n'est désactivé dans le projet.
- API 36 : Emulator 37.2.12, Android 16, pages de 4 096 octets. Installation du binaire avec l'identité corrigée, version 1.5.8/code 2, puis démarrage à froid : succès. Restore par le fournisseur de documents, sélection depuis Downloads, redémarrage, clavier virtuel et paysage : succès. Le clavier est réellement affiché (`mInputShown=true`, `mIsInputViewShown=true`) et les boutons restent au-dessus de ses limites.
- Sources et preuves API 21 : `.local/step08-api21-fix-build.log`, `step08-api21-after-fix.xml`, `step08-api21-three-boxes-settled.xml`, `step08-api21-keyboard.xml`, `step08-api21-landscape.xml`, `step08-api21-final.db3` et journaux du moteur.
- Sources et preuves API 36 : `.local/step08-android-identity-build.log`, `step08-api36-first-launch.xml`, `step08-api36-keyboard-visible.xml`, `step08-api36-landscape.xml`, `step08-api36-final-keyboard.xml` et captures correspondantes. Aucun appel OpenAI.

Compilation du code 2 avec la bonne identité : `dotnet build SmallBazar.csproj -m:1 -t:Rebuild -f net10.0-android -c Release`, réussite avec zéro erreur et un avertissement CS0219 existant. APK `com.companyname.smallbazar-Signed.apk`, SHA-256 `C7029F96D9C6A9093DD14513CB30D653BD284E87A142DED40601A6F3D4EE2740`. Les 230 bibliothèques 64 bits passent le contrôle ELF et `zipalign -c -P 16 4` retourne 0 ; `.local/step08-identity-alignment.json`. Les règles de sauvegarde utilisent maintenant le préfixe réel `com.companyname.smallbazar`.

Windows Release non empaqueté, après la correction conditionnelle d'identité Android : réussite, zéro erreur, 37 avertissements préexistants ; `.local/step08-windows-final-build.log`.

## Sauvegardes et préparation de la réinstallation du smartphone

L'ancienne application produit une sauvegarde fraîche `BigBazar.Bak.db3`. Après autorisation explicite de l'utilisateur, Owlfiles l'enregistre dans le dossier Download local, puis ADB la copie dans le dossier de sauvegarde PC, sans upload ni envoi à un destinataire. `BigBazar-before-reinstall.db3` contient 71 boîtes, 11 emplacements, 37 anciennes catégories et 18 associations, version de schéma 0, `integrity_check=ok`. SHA-256 `BD8E306580FAFA33DDC7B5A578618B8F7391EF325BF2DD2C11D2C3755FCA4232`.

Une copie est restaurée sur API 36 : toutes les valeurs de `Boxes` et `Areas` sont identiques, intégrité correcte, schéma 1, compteur UI 71. `.local/step08-phone-restoration-check.json` en conserve les résultats. La sauvegarde originale n'est pas migrée ni écrasée.

L'utilisateur précise ensuite que Windows doit être la source la plus récente. L'ancienne application Windows a été désinstallée et la nouvelle n'est pas installée. La base encore présente dans `AppData/Local/User Name/com.enaxos.smallbazar.validation/Data` et la sauvegarde de référence `E:/Backups/SmallBazar/20261015T234507815478Z/SmallBazar-installed/BigBazar.db3` sont strictement identiques : SHA-256 `58ADB1E5486B0195811B53E83EB35338379EA22AD8A905F9184804C71B8F76D6`, 72 boîtes et 11 emplacements. Cette dernière source Windows devient donc la source de restauration du téléphone ; la sauvegarde Android de 71 boîtes reste un secours séparé.

La copie `Windows-source-72-boxes.db3` est conservée dans le dossier de sauvegarde PC et transférée sur le téléphone sous `Download/SmallBazar-Windows-72-boxes.db3`. Son empreinte sur le téléphone est vérifiée. Un second essai de restauration sur API 36 confirme le compteur UI 72, toutes les valeurs de boîtes et emplacements identiques et l'intégrité correcte ; `.local/step08-windows-restoration-check.json`.

Après ces contrôles et l'autorisation utilisateur, l'ancienne application est désinstallée. Xiaomi refuse l'installation ADB avec `INSTALL_FAILED_USER_RESTRICTED`. L'APK est donc copié dans `Download/SmallBazar-1.5.8.apk`, empreinte vérifiée, et l'utilisateur choisit de l'installer localement depuis ce fichier. L’installation locale du code 2 et la restauration sont ensuite confirmées ; voir les résultats de clôture ci-dessous.

## Clôture sur smartphone et correction du thème sombre

L’installation locale de la version 1.5.8/code 2 est confirmée par Android. La restauration est effectuée dans l’application depuis `Download/SmallBazar-Windows-72-boxes.db3` : le compteur affiche 72 boîtes. Une nouvelle sauvegarde est produite par Save. Après autorisation explicite pour ce nouveau contenu, Owlfiles l’enregistre localement dans Download et ADB la copie par USB vers `Phone-restored-Windows-verification.db3`, sous le dossier de sauvegarde PC. Aucun envoi réseau n’est effectué.

Cette sauvegarde issue du téléphone physique est comparée en lecture seule à `Windows-source-72-boxes.db3` : toutes les colonnes de toutes les lignes de `Boxes` et `Areas`, ordonnées par identifiant, sont identiques. Résultat : 72 boîtes, 11 emplacements, `integrity_check=ok`, schéma 1. Preuve locale : `.local/step08-phone-windows-final-check.json`. Les sauvegardes antérieures et les APK historiques restent conservés séparément.

Le signalement utilisateur sur le thème sombre révèle plusieurs couleurs fixes incompatibles : cartes Android blanches, liste blanche, texte noir, éditeur clair et mode natif forcé en clair. Les corrections suivent le thème système (`AppTheme.Unspecified` et `ModeNightFollowSystem`), utilisent des couleurs adaptées aux deux thèmes pour les surfaces, textes, numéros, sélection, titres et onglets, et donnent un texte sombre aux boutons à fond bleu clair. La connexion OpenAI reste inchangée.

Recette visuelle du code 3 sur API 36 : liste, recherche, outils et détail ; passage sombre → clair → sombre pendant l’exécution, sans relancement forcé ; fonds et textes réagissent correctement. Captures locales stabilisées : `.local/step08-theme-final-dark-list.png`, `step08-theme-final-dark-search.png`, `step08-theme-final-dark-tools.png`, `step08-theme-light-detail-settled.png`, `step08-theme-dark-detail-settled.png` et `step08-theme-light-tools-settled.png`. Les captures portent sur trois boîtes synthétiques.

La mise à jour `adb install -r` du téléphone vers le code 3 réussit cette fois, sans désinstallation. Android confirme `versionName=1.5.8`, `versionCode=3`, `minSdk=21`, `targetSdk=36` ; la date de première installation est conservée. Le démarrage réussit et le compteur reste à 72. Le téléphone conserve son thème système sombre ; Tools et Search sont contrôlés visuellement, avec cartes sombres et textes lisibles. Preuves : `.local/step08-phone-build3-list.xml`, `step08-phone-build3-dark-tools.png`, `step08-phone-build3-dark-search.png`. Cette mise à jour ne change aucun traitement de données ; la comparaison intégrale provient de la sauvegarde réalisée juste avant, sous le code 2.

### Binaire final et vérifications

- Android : `dotnet build SmallBazar.csproj -m:1 -t:Rebuild -f net10.0-android -c Release`, zéro erreur, un avertissement CS0219 préexistant. Journal `.local/step08-theme-android-build-final.log`.
- Windows : compilation Release non empaquetée avec `-m:1`, zéro erreur, 35 avertissements préexistants. Journal `.local/step08-theme-windows-build-final.log`. L’installation Windows et sa publication restent dans l’étape 09.
- APK final : `bin/Release/net10.0-android/com.companyname.smallbazar-Signed.apk`, version 1.5.8/code 3, SHA-256 `18F2E32EEFC46C73EC5AB5D7D0CA6F6502D0FEA74C4B8AAE5042B8C842690085`.
- Signature vérifiée : SHA-256 `31a9067770d340c953eecd86f7c5efa6201b3d886e6460f705c7a33cc0773f3c`, identique au code 2 installé. Preuve `.local/step08-theme-signature.txt`.
- Contrôle ELF des 230 bibliothèques 64 bits sans anomalie, puis `zipalign -c -P 16 4` : code retour 0. Preuve `.local/step08-theme-native-alignment.json`.
- Copie durable du binaire : `E:/Backups/SmallBazar/20261016T2008286368917Z/Android-installed/SmallBazar-1.5.8-build3-new-signature.apk`. Le nouveau magasin privé, explicitement autorisé, est conservé hors Git dans `New-Android-signing/debug.keystore` au même emplacement.

L’exception de remplacement de l’ancienne signature et la reconfiguration ultérieure de la clé OpenAI sont acceptées par l’utilisateur. Les essais ne prétendent pas couvrir une sauvegarde cloud réelle ni la conservation de l’ancien secret. L’affichage historique 1.6.4 dans Tools reste à harmoniser à l’étape 09 ; les métadonnées du paquet installé sont bien 1.5.8/code 3.

## Sources officielles

- [MAUI 10 : prise en charge Android API 36](https://learn.microsoft.com/en-us/dotnet/maui/whats-new/dotnet-10?view=net-maui-10.0).
- [MAUI 10 : zones sûres et clavier](https://learn.microsoft.com/en-us/dotnet/maui/user-interface/safe-area?view=net-maui-10.0).
- [MAUI : mode de redimensionnement du clavier Android](https://learn.microsoft.com/en-us/dotnet/maui/android/platform-specifics/soft-keyboard-input-mode?view=net-maui-10.0).
- [MAUI 10 : configuration mailto](https://learn.microsoft.com/en-us/dotnet/maui/platform-integration/communication/email?view=net-maui-10.0).
- [MAUI : stockage sécurisé](https://learn.microsoft.com/en-us/dotnet/maui/platform-integration/storage/secure-storage?view=net-maui-10.0&tabs=android).
- [Android : règles Auto Backup et transfert entre appareils](https://developer.android.com/identity/data/autobackup).
- [MAUI 10.0.110 : nom réel du conteneur de préférences](https://github.com/dotnet/maui/blob/10.0.110/src/Essentials/src/Preferences/Preferences.shared.cs).
