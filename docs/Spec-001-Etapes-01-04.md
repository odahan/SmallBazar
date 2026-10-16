# Spec-001 — Compte rendu des étapes 01 à 04

Date : 16 octobre 2026, Europe/Paris. Référence Git de départ : `2ea9b42c70039944f1b916f730af83af3de3621e`.

## Contrôle préalable des sections 1 à 4

- Objectif confirmé : migration native MAUI Android/Windows vers .NET 10, avec conservation des boîtes et emplacements.
- Périmètre confirmé : anglais, MVVM Toolkit, onglets et CollectionView conservés. OpenAI 2.4.0, modèle, prompt, connexion et rapport conservés ; aucun appel distant effectué.
- Les tables historiques restent présentes à ce stade. Leur retrait appartient exclusivement au lot 06B, après sauvegarde et validation du lot 06A. Le paquet SQLite et son bundle restent ceux de la référence tant que le démarrage ne nécessite pas leur remplacement.
- État de départ vérifié : projet .NET 8, SDK 10 disponible, sauvegardes intègres. L'écran noir signalé est une limite de la référence fonctionnelle, pas une conséquence démontrée du nettoyage.
- Identités source inchangées : ApplicationId `com.enaxos.smallbazar`, Publisher `CN=enaxos`, minimum Android 21 et minimum Windows 10.0.17763.0. Les options de distribution et de signature ne sont pas modernisées dans ce lot.
- Versions et compatibilités revérifiées dans les sources officielles ci-dessous, puis restauration et compilations contrôlées sur le poste. L'installation des outils est distincte de la validation fonctionnelle des appareils.

## Étape 01 — Référence, sauvegardes et écran noir

Les trois bases du manifeste `E:\Backups\SmallBazar\20261015T234507815478Z\manifest.json` ont été ouvertes en lecture seule. Leurs empreintes SHA-256 correspondent au manifeste, `PRAGMA integrity_check` retourne `ok` et les comptages restent conformes :

| Base | Boîtes | Emplacements | Catégories | Associations |
| --- | ---: | ---: | ---: | ---: |
| SmallBazar installé, sauvegarde | 72 | 11 | 37 | 18 |
| SmallBazar, sauvegarde antérieure | 71 | 11 | 37 | 18 |
| BigBazar historique | 30 | Table absente | 30 | 120 |

Copies de travail : `.local/reference-20261016/`, exclu de Git. Les références ne sont ni modifiées ni écrasées. La création d'un nouveau dossier sous `E:\Backups\SmallBazar` a été refusée par le système de fichiers ; les copies ont donc été placées dans le projet sous `.local`.

Le script `scripts/verify_reference_databases.py` permet de refaire cette vérification avec Python 3 et sa bibliothèque standard. Il impose un nouveau répertoire de sortie et crée les copies avec l'API de sauvegarde SQLite.

### Identités et modes de lancement observés

| Origine | Identité/version | Observation |
| --- | --- | --- |
| Paquet installé identifié par Windows Error Reporting | `com.enaxos.smallbazar_1.6.5.0_x64__24t49ffww27ep`, application `App` | Identité complète issue des événements ; enregistrement non retourné par `Get-AppxPackage` dans la session d'exécution |
| Famille de paquet de référence | `com.enaxos.smallbazar_24t49ffww27ep` | Conforme au chemin de base sauvegardé |
| Manifeste source Windows | Publisher `CN=enaxos`, version `1.6.5.0` | Le nom placeholder est remplacé par l'ApplicationId à la génération |
| Sortie locale Debug .NET 8 / win10-x64 | `1.6.4.0`, 9 septembre 2025 | Ancien manifeste, distinct de l'installation et de la compilation courante |
| Sortie locale Release .NET 8 / win10-x64 | `1.5.1.0`, 27 novembre 2024 | Ancienne sortie |
| Sortie locale x64/Release .NET 8 / win10-x64 | `1.6.5.0`, 9 septembre 2025 | Ancienne sortie |
| Lancement IDE | Profil `Windows Machine`, `commandName: MsixPackage` | Mode empaqueté ; exécution IDE non reproduite dans cette session |
| Publication existante | Release x64, ancien RID `win10-x64` | Profil incohérent hérité ; correction réservée à l'étape 09 |

### Incident constaté

Le journal Application contient trois événements Windows Error Reporting 1001 le 16 octobre 2026 à 01:41:02, 01:41:22 et 01:43:22, heure de Paris. Le type est `MoAppHangXProc`, avec le paquet `1.6.5.0`. Un événement cite `SearchIndexer.exe` dans sa signature de blocage interprocessus ; cela ne démontre pas une cause.

La base active et ses logs ne sont pas accessibles au chemin personnel indiqué dans la session d'exécution. Aucun diagnostic de pile ne permet d'attribuer l'écran noir à SQLite, au nettoyage ou à l'indexeur. L'ancienne application installée n'a pas été relancée, désinstallée ni réinitialisée. Les essais de la version migrée sont isolés et ne constituent pas une recette de mise à jour MSIX.

### Parcours à conserver

Liste, tri et sélection des boîtes ; détail et précédent/suivant ; création, modification, suppression et numérotation ; recherche ; gestion des emplacements ; sauvegarde/restauration ; export ; maintenance, logs et liens externes. Les fonctions du rapport IA restent présentes, avec leur compilation pour seule validation dans cette phase. La référence installée ne permet pas de déclarer ces parcours déjà fonctionnels. Leur recette complète appartient aux étapes suivantes.

## Étape 02 — Chaîne de développement

`global.json` sélectionne exactement le SDK `10.0.401`, refuse les préversions et désactive le roll-forward. Il fixe également le workload set `10.0.401`, installé avec `dotnet workload install maui-windows maui-android`. Avant cette installation, le poste utilisait des manifestes Visual Studio sans workload set enregistré.

| Outil | Version contrôlée |
| --- | --- |
| SDK .NET | 10.0.401 |
| MSBuild du SDK | 18.9.11 |
| Workload set | 10.0.401 |
| Manifeste MAUI / pack SDK MAUI | 10.0.20 / bande 10.0.100 |
| Manifeste Android | 36.1.69 / bande 10.0.100 |
| Visual Studio | 18.10.12224.181 |
| SDK Android installé | plateformes 35 et 36 ; build-tools 36.0.0 disponibles |
| JDK contrôlé | Microsoft OpenJDK 21.0.8 ; JDK 17 également présent |
| ADB | 1.0.41, platform-tools 36.0.0 |
| Outils Windows via NuGet | Microsoft.Windows.SDK.BuildTools 10.0.26100.8249 restauré |

Le SDK Windows système autonome n'est pas détecté dans l'emplacement conventionnel. La compilation Windows utilise les outils fournis par le graphe NuGet restauré et réussit. Les chemins locaux Android/JDK ne sont pas enregistrés dans le csproj ; la détection automatique du workload a permis la compilation Android.

`NuGet.Config` conserve uniquement nuget.org et vide les dossiers de repli. Les versions restent explicites dans le csproj, avec `MauiVersion` dans `Directory.Packages.props` et `ManagePackageVersionsCentrally=false`.

## Étape 03 — Socle MAUI

| Dépendance | Version retenue |
| --- | --- |
| Microsoft.Maui.Controls | 10.0.110 |
| CommunityToolkit.Maui | 15.0.1 |
| CommunityToolkit.Mvvm | 8.4.2 |
| Microsoft.Extensions.Logging.Debug | 10.0.0 |
| OpenAI | 2.4.0, conservée |
| sqlite-net-pcl | 1.9.172, conservée pour le lot 06A |
| SQLitePCLRaw.bundle_green | 2.1.10, conservé pour le lot 06A |

Les deux TFM ciblent .NET 10. Controls.Compatibility a été retiré après recherche d'usages. Les fichiers générés sont émis sous `obj/generated/<Configuration>/<TargetFramework>`, sans dépendance à `SolutionDir`. L'ancien `DummyGenerated` est explicitement exclu de la compilation : sa présence locale provoquait des doublons avec les nouveaux générateurs. Les sources des tests sont aussi exclues du projet MAUI.

La restauration et les compilations Debug Android/Windows ont réussi avec zéro erreur. Aucun changement du code OpenAI n'a été nécessaire. Les versions de paquet ne sont plus flottantes. Les sorties et graphes locaux restent exclus de Git.

Limites enregistrées : avertissements d'obsolescence MAUI (Frame, alertes, animations, MainPage) à traiter dans les lots concernés ; MVVMTK0045 pour les propriétés générées par champs dans les scénarios WinRT/AOT, sans activation d'AOT dans ce lot ; certificat Windows expiré (APPX0108). L'audit NuGet confirme la vulnérabilité élevée `GHSA-2m69-gcr7-jv3q` de SQLitePCLRaw.lib.e_sqlite3 2.1.10 sur Windows et de sa variante Android. Son traitement appartient au lot 06A, qui reste ouvert avant livraison.

## Étape 04 — Démarrage et chargement

- La page racine est résolue dans `App.CreateWindow`, avec titre et dimensions 800 × 800 conservés.
- DialogService suit la fenêtre activée et sa page modale ; les dialogues passent sur le thread principal.
- DataService ne lance plus de création de tables depuis son constructeur. `InitializeAsync` sérialise l'initialisation, attend chaque opération de schéma et le PRAGMA, puis positionne `IsInitialized` après succès. En cas d'erreur, l'état reste faux et l'exception remonte ; un appel suivant peut réessayer.
- Les opérations publiques de données attendent cette initialisation. La création de tables utilisée en maintenance suit le même chemin.
- GlobalViewModel ne charge plus les données dans un `Task.Run` abandonné. MainPage attend son initialisation depuis `Loaded`, après attachement natif de la page, observe les erreurs, les journalise et affiche une alerte. Le chargement et les modifications des propriétés liées aux vues passent sur le thread principal.
- Les attentes `.Wait()` du tri et des gestionnaires d'exceptions ont été retirées. Les `ConfigureAwait(false)` du GlobalViewModel ont été supprimés pour conserver le contexte UI.
- ServiceHelper reste initialisé immédiatement après la construction du conteneur, avant résolution de la page racine. Aucun service de données ne tente de résoudre MainPage depuis son constructeur.

### Preuves de validation

Cinq tests SQLite natifs : création d'une base vide et réexécutions concurrentes ; propagation d'une erreur de base invalide et nouvel essai après réparation ; initialisation répétée de bases de 72, 71 et 30 boîtes. Les tests historiques ont aussi été exécutés sur de nouvelles copies des trois sauvegardes réelles : mêmes valeurs des colonnes d'origine de Boxes, Areas, Categories et BoxCats, intégrité correcte. La base BigBazar ne contient initialement ni Areas ni les colonnes AreaCode/CreatedAt ; SQLite-net ajoute ces colonnes avec NULL et crée Areas vide, sans modifier les valeurs existantes.

Windows : lancement et capture visuelle d'une version Debug non empaquetée, avec l'ApplicationId de validation `com.enaxos.smallbazar.validation` et des répertoires intermédiaires/sorties distincts. Base vide : quatre onglets et commandes visibles, compteur 0. Copie de la sauvegarde principale : liste visible et compteur 72. Aucun écran noir persistant dans ces essais. Une base volontairement invalide a aussi été essayée dans ce stockage isolé : erreur journalisée, alerte « Startup error » visible et fichier invalide laissé intact. Le test a révélé que l’alerte ne doit pas être présentée depuis OnAppearing avant attachement natif ; le déclenchement a été déplacé vers Loaded. Après remise de la copie saine et redémarrage, le compteur revient à 72. Le stockage de validation est séparé du paquet personnel. Ce résultat ne valide ni l'ancien binaire, ni le lancement MSIX, ni la mise à jour installée.

Android : compilation Debug réussie et lancement vérifié par captures d’écran sur un nouvel émulateur dédié Android 15 / API 35, x86_64, image google_apis_playstore, créé sous `.local/avds` (nom `smallbazar-spec001`, port 5580). Base vide : onglets et compteur 0 visibles. Copie de la sauvegarde principale : liste et compteur 72 visibles. Après démarrage, la base a été relue avec `run-as` et comparée intégralement à la copie de référence : valeurs identiques pour Boxes (72), Areas (11), Categories (37), BoxCats (18), intégrité `ok`. Aucun appareil personnel n’a été utilisé.

Le premier APK Debug dépendait du Fast Deployment de l’IDE et quittait avec « No assemblies found ». Le paquet destiné au lancement direct via ADB a été reconstruit avec `EmbedAssembliesIntoApk=true`, sans modifier le comportement Debug par défaut du projet. L’installation finale et les lancements retournent `Success` / `Status: ok`. Les captures locales sont `.local/android-empty.png` et `.local/android-72-boxes.png` ; elles contiennent des données personnelles et restent exclues de Git.

Ces vérifications ne remplacent pas les essais sur appareil physique, les essais Release ou les mises à jour en place. Les validations des étapes 08 à 10 restent ouvertes.

## Commandes reproductibles

Depuis la racine du projet, sans paramètre temporaire `CheckEolWorkloads` :

```powershell
dotnet --version
dotnet workload --version
dotnet workload list
dotnet workload install maui-windows maui-android
dotnet restore SmallBazar.csproj --disable-parallel
dotnet build SmallBazar.csproj -m:1 -f net10.0-windows10.0.19041.0 -c Debug --no-restore
dotnet build SmallBazar.csproj -m:1 -f net10.0-android -c Debug --no-restore
dotnet test tests/SmallBazar.Data.Tests/SmallBazar.Data.Tests.csproj -m:1
dotnet list SmallBazar.csproj package --vulnerable --include-transitive --no-restore
```

Vérification et copie des références, avec un nouveau dossier à chaque essai :

```powershell
python scripts/verify_reference_databases.py --manifest E:/Backups/SmallBazar/20261015T234507815478Z/manifest.json --output-directory .local/reference-new
$env:SMALLBAZAR_REFERENCE_DIRECTORY = (Resolve-Path .local/reference-new).Path
dotnet test tests/SmallBazar.Data.Tests/SmallBazar.Data.Tests.csproj -m:1
Remove-Item Env:SMALLBAZAR_REFERENCE_DIRECTORY
```

Sans cette variable, les tests utilisent exclusivement des données synthétiques. Avec la variable, ils recopient chaque référence dans un nouveau fichier temporaire avant d'exécuter l'initialisation.

Commande du lancement Windows isolé utilisé pour la recette de démarrage :

```powershell
dotnet build SmallBazar.csproj -m:1 -f net10.0-windows10.0.19041.0 -c Debug --no-restore -p:WindowsPackageType=None -p:AppxPackageSigningEnabled=false -p:GenerateAppInstallerFile=false -p:ApplicationId=com.enaxos.smallbazar.validation -p:IntermediateOutputPath=obj/windows-smoke/ -p:OutputPath=.local/windows-smoke/
```

Commande Android pour un APK Debug lançable directement hors IDE :

```powershell
dotnet build SmallBazar.csproj -m:1 -f net10.0-android -c Debug --no-restore -p:EmbedAssembliesIntoApk=true
```

Pour les actions ADB, sélectionner explicitement le numéro de série de l’émulateur de validation ; celui utilisé ici est `emulator-5580`. Le nom de MainActivity doit être résolu depuis le paquet installé, sans supposer qu’il reste identique entre compilations.

Vérifier l'identité générée avant tout lancement isolé ; ne jamais déduire l'identité d'un ancien répertoire de compilation. Les logs de compilation, l'audit NuGet et les vérifications de copies de cette session sont dans `.local`, exclus du dépôt.

## Sources officielles consultées

- [Sélection du SDK et roll-forward dans global.json](https://learn.microsoft.com/en-us/dotnet/core/tools/global-json).
- [Workload sets et épinglage dans global.json](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-workload-sets).
- [Microsoft.Maui.Controls 10.0.110](https://www.nuget.org/packages/Microsoft.Maui.Controls/10.0.110).
- [CommunityToolkit.Maui 15.0.1 et dépendance Controls >= 10.0.90](https://www.nuget.org/packages/CommunityToolkit.Maui/15.0.1).
- [CommunityToolkit.Mvvm 8.4.2](https://www.nuget.org/packages/CommunityToolkit.Mvvm/8.4.2).
- [Microsoft.Extensions.Logging.Debug 10.0.0](https://www.nuget.org/packages/Microsoft.Extensions.Logging.Debug/10.0.0).
