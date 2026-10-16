# Spec-001 — Lot 06B : retrait des catégories historiques

Date : 16 octobre 2026, Europe/Paris. Statut : implémentation et tests natifs terminés ; recette interactive et échange Android/Windows restant à confirmer.

## Périmètre réalisé

Les modèles persistés `Category` et `BoxCat` sont supprimés, ainsi que leurs méthodes dans `IDataService` et `DataService`. La suppression de boîte ne lit plus la jointure historique. Le démarrage ne charge plus de catégories ; les propriétés de sélection, l'état de recherche par catégorie et le filtre XAML commenté sont retirés.

L'export ne contient plus le bloc historique commenté. La génération de démonstration insère uniquement des boîtes et emplacements. Création des tables, réinitialisation et restauration passent par le nouveau schéma. Les noms des anciennes tables ne subsistent dans le code applicatif que dans la migration.

`CategoryReport`, les catégories produites par IA, le cache, le prompt et le code OpenAI sont conservés sans modification.

## Version et migration

`DatabaseSchema.CurrentVersion = 1`, enregistré dans `PRAGMA user_version`. Les bases historiques ont la version 0. Une version supérieure à celle supportée est refusée avant toute modification.

Pour une base de version 0, l'initialisation inventorie les tables réellement présentes dans `sqlite_master`. Si la base contient déjà des tables métier, `BackupAsync` crée d'abord un instantané cohérent, y compris les données validées dans le WAL. La sauvegarde est conservée dans `DatabaseMigrationBackups`, à côté de la base, avec un nom horodaté et un GUID ; son `integrity_check` doit réussir avant toute modification du schéma. Un échec de sauvegarde bloque la migration. Les sauvegardes de référence restent intactes.

La transaction SQLite unique crée ou complète `Boxes` et `Areas` selon les règles SQLite-net existantes, supprime `BoxCats` puis `Categories`, puis fixe `user_version = 1`. La validation de cette transaction rend la version et les changements visibles ensemble. Une exception annule aussi la première suppression et la création d'une table manquante ; `DatabaseStore.IsInitialized` reste faux et la connexion est fermée, permettant un nouvel essai après réparation.

Une base neuve ne crée que `Boxes` et `Areas`, en plus de `sqlite_sequence`. Une ancienne base BigBazar dépourvue d'emplacements reçoit une table `Areas` vide ; ses colonnes de boîtes initiales sont conservées. Une base déjà en version 1 ne produit aucune nouvelle sauvegarde de migration et ne rejoue pas le changement de schéma.

Les accès applicatifs, l'initialisation et la migration restent protégés par le verrou de `DatabaseStore` introduit en 06A. Aucune base n'est supprimée puis recréée pour effectuer la migration.

## Restauration

L'ancienne sauvegarde est copiée depuis le flux dans le fichier de préparation, contrôlée, sauvegardée avant migration si nécessaire, puis migrée et relue avant remplacement de la base active. Les lectures de validation portent seulement sur `Box` et `Area`.

L'instantané de migration de la sauvegarde importée conserve les anciennes catégories pour récupération. La copie de secours de la base active avant restauration reste distincte, dans `DatabaseRecovery`. Les anciennes sauvegardes sans catégories et les sauvegardes déjà en version 1 sont acceptées ; le second format est couvert par un aller-retour sauvegarde/restauration. Les fichiers de préparation sont nettoyés, et les instantanés de migration sont conservés sans purge automatique.

## Tests natifs

25 tests réussis sur données synthétiques, puis 25 réussis avec `SMALLBAZAR_REFERENCE_DIRECTORY` pointant sur les copies préparées en 06A. Chaque essai travaille sur de nouveaux fichiers temporaires ; les tests de schéma isolent et nettoient aussi leurs propres instantanés de migration.

- Base vide, initialisations répétées et second lancement avec nouvelle instance du store : schéma version 1, uniquement les deux tables utiles, absence de nouvelle migration.
- Copies des références de 72 et 71 boîtes : comparaison SQL de toutes les valeurs des colonnes initiales de `Boxes` et `Areas`, avec identifiants, numéros et dates ; catégories et jointures absentes, intégrité correcte.
- Ancienne base de 30 boîtes : mêmes valeurs dans les colonnes d'origine, création d'`Areas` vide.
- Instantané avant migration contenant encore les catégories et associations, version 0 et données validées dans le WAL ; contrôle d'intégrité réussi.
- Échec provoqué après la suppression de `BoxCats`, en présentant `Categories` comme une vue : rollback des tables et de la version, données conservées, initialisation non terminée, réparation puis nouvel essai réussi.
- Échec de création du répertoire de sauvegarde : aucune modification du schéma ; nouvel essai réussi après réparation.
- Version future refusée sans modification des tables ou de la version.
- Restauration par flux non repositionnable avec migration, conservation de l'instantané historique et secours frais de la base active.
- Aller-retour ancienne sauvegarde → migration → sauvegarde version 1 → restauration, pour bases vide/72/71/30 boîtes.
- Tests 06A conservés : fichier invalide, annulation, interruption de flux, remplacement refusé, sérialisation, WAL et connexion utilisable après erreur.

Journaux locaux : `.local/step06b-tests-native.log` et `.local/step06b-tests-reference.log`. Le moteur de tests nécessite une exécution hors bac à sable pour établir sa communication locale ; les essais natifs y passent sans modification du code pour contourner cette restriction.

## Compilation et limites de recette

Windows Release non empaqueté : compilation réussie, zéro erreur. Journal : `.local/step06b-build-windows-release.log`. La première génération complète conserve les avertissements préexistants MVVM/WinRT et du rapport ; le passage incrémental suivant ne produit aucun avertissement.

Android Release : reconstruction complète réussie avec l'identité de validation `com.enaxos.smallbazar.sqlitevalidation`, AOT et trimming conservés ; zéro erreur, un avertissement préexistant CS0219 dans le rapport IA. Journal : `.local/step06b-build-android-release.log`. L'APK est produit sans installation sur un appareil ou émulateur dans ce lot.

Les bases personnelles installées n'ont pas été ouvertes par la nouvelle application. Aucun paquet n'a été installé ou publié dans ce lot.

La recette interactive de l'export, de la suppression de boîte et de la réinitialisation sur les deux plateformes, ainsi que l'échange effectif d'un fichier de sauvegarde entre Android et Windows, restent à confirmer. Les tests natifs vérifient les mêmes fichiers SQLite et leur restauration, mais ne constituent pas une recette de ces parcours UI. Les résultats Android et les commandes de compilation sont consignés ci-dessous.

## Commandes reproductibles

Depuis la racine du projet :

```powershell
dotnet test tests/SmallBazar.Data.Tests/SmallBazar.Data.Tests.csproj -m:1 --no-restore
$env:SMALLBAZAR_REFERENCE_DIRECTORY = (Resolve-Path .local/reference-step06a).Path
dotnet test tests/SmallBazar.Data.Tests/SmallBazar.Data.Tests.csproj -m:1 --no-restore
Remove-Item Env:SMALLBAZAR_REFERENCE_DIRECTORY

dotnet build SmallBazar.csproj -m:1 -f net10.0-windows10.0.19041.0 -c Release --no-restore -p:WindowsPackageType=None -p:AppxPackageSigningEnabled=false -p:GenerateAppInstallerFile=false -p:IntermediateOutputPath=obj/windows-sqlite-release/ -p:OutputPath=.local/windows-sqlite-release/

dotnet build SmallBazar.csproj -m:1 -t:Rebuild -f net10.0-android -c Release --no-restore -p:ApplicationId=com.enaxos.smallbazar.sqlitevalidation
```

Les dépendances doivent avoir été restaurées pour la configuration choisie. Un premier essai Windows Debug avec les assets issus d'une restauration Release échoue sur `AddDebug` : restaurer pour Debug avant de changer de configuration. Les compilateurs natifs et le moteur de tests doivent pouvoir lancer leurs processus auxiliaires ; un échec MSBuild `MSB4216` dans le bac à sable ne constitue pas une erreur du code C#.
