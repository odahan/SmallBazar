# Audit de modernisation de SmallBazar

Date : 16 octobre 2026. Périmètre : mise à niveau technique à fonctionnalités identiques sur Android et Windows.

> Périmètre révisé après l'audit : la [spécification de phase 1](docs/Spec-001-Dotnet10.md) fait foi pour la mise en œuvre. L'utilisateur demande de supprimer les tables historiques `Categories` et `BoxCats`, en préservant les boîtes et emplacements par une migration sauvegardée et versionnée. L'intégration OpenAI reste telle quelle : seules les adaptations nécessaires à la compilation sont prévues ; sa mise à jour et sa refonte sont reportées. Les recommandations initiales ci-dessous qui prévoyaient la conservation de toutes les tables ou une modernisation OpenAI complète sont donc remplacées par ces décisions.

Le choix de `sqlite-net-pcl` a été confirmé dans la [documentation Microsoft MAUI 10 fournie par l'utilisateur](https://learn.microsoft.com/fr-fr/dotnet/maui/data-cloud/database-sqlite?view=net-maui-10.0#install-the-sqlite-nuget-package) : auteur SQLite-net, propriétaire praeclarum. `Microsoft.Data.Sqlite` y figure comme une alternative ADO.NET, qui n'est pas retenue pour SmallBazar. La vérification des dépendances natives lors de la mise à jour de sqlite-net-pcl reste nécessaire.

## Conclusion

La cible recommandée aujourd'hui est .NET 10 / .NET MAUI 10, avec des versions stables et explicitement figées des dépendances. L'application possède déjà les fondations utiles : projet MAUI unique, MVVM Toolkit, injection de dépendances, services abstraits et CollectionView. Une réécriture générale n'est pas justifiée par cet audit.

La migration comporte cependant trois zones sensibles : la nouvelle chaîne native SQLite, les changements de comportement MAUI/Community Toolkit, et la conservation des données et de l'identité des applications installées. La remise à plat de la publication Windows fait également partie du travail nécessaire.

MAUI 8 est hors support depuis le 14 mai 2025 et MAUI 9 depuis le 12 mai 2026. La politique officielle consultée indique MAUI 10.0.110, avec une fin de support au 11 mai 2027. Le support de MAUI est distinct du support LTS du runtime .NET : il faudra continuer à suivre les mises à jour MAUI après cette migration. [Politique MAUI](https://dotnet.microsoft.com/en-us/platform/support/policy/maui).

## Méthode et limites

- Lecture du projet, des références NuGet, du démarrage, des services, modèles, ViewModels, vues XAML, intégrations natives et fichiers de publication.
- Lecture de `obj/project.assets.json` pour identifier les versions précédemment résolues. Ce fichier est un état local de restauration, pas une preuve de compilation actuelle.
- Vérification des versions sur NuGet et des changements dans les documentations officielles.
- Tentative de compilation Windows sans restauration : `dotnet build SmallBazar.csproj -m:1 -f net8.0-windows10.0.19041.0 --no-restore -v:minimal`.
- Échec avant compilation du code : `ResolvePackageAssets` / `MSB4018`, dossier de repli NuGet absent `C:\Program Files (x86)\Syncfusion\Essential Studio\WPF\30.1.37\ToolboxNuGetPackages`, référencé par les assets locaux. Cela ne démontre pas une erreur dans les sources SmallBazar et ne signifie pas que SmallBazar dépend de Syncfusion.
- Le SDK sélectionné localement est 10.0.401 ; plusieurs SDK 8, 9 et 10 sont installés. Pas de `global.json` trouvé à la racine du projet ou dans les informations SDK. `dotnet --info` fournit un inventaire partiel, mais sa lecture des workloads et `dotnet workload list` échouent faute d'accès au gestionnaire de services Windows. L'état opérationnel des workloads MAUI n'est donc pas confirmé.
- Aucun projet de tests ni pipeline CI versionné identifié dans les fichiers suivis examinés. Aucun lancement de l'application, aucune recette sur appareil, aucun appel OpenAI payant et aucune publication effectués.
- Aucune modification du code applicatif ni des versions de paquets dans cet audit.

## Suivi : nettoyage NuGet effectué le 16 octobre 2026

La référence Syncfusion identifiée pendant l'audit ne correspondait à aucun paquet applicatif direct ou transitif, ni à un usage dans les sources. Elle provenait des anciens fichiers de restauration dans `obj`, qui mémorisaient un dossier de repli machine désormais absent.

Un `NuGet.Config` local utilise maintenant uniquement nuget.org et vide les dossiers de repli hérités de la configuration machine. Le dossier généré `obj` a été supprimé puis régénéré par restauration. Les nouveaux assets ne contiennent plus aucune référence Syncfusion. La configuration NuGet globale du poste n'a pas été modifiée.

Validation : `dotnet restore SmallBazar.csproj -p:CheckEolWorkloads=false --verbosity minimal` réussit. La désactivation du contrôle de fin de support concernait uniquement cette commande, afin de restaurer l'ancienne cible Android .NET 8 avec le SDK 10 ; elle n'a pas été enregistrée dans le projet. La compilation Windows `dotnet build SmallBazar.csproj -m:1 -f net8.0-windows10.0.19041.0 --no-restore -v:minimal` réussit avec zéro erreur. Les avertissements restants concernent la variable `options` inutilisée dans CategoryReport et les anciens RID WindowsAppSDK. Aucune compilation Android ni exécution de l'application n'a été effectuée dans cette étape.

Les limites de compilation décrites dans la section précédente correspondent donc à l'état initial de l'audit ; le blocage NuGet a été levé.

## État et versions cibles

Les versions cibles ci-dessous sont celles des pages officielles consultées le 16 octobre 2026. Elles constituent une base de migration à valider ensemble par restauration et compilation ; les mises à jour de maintenance disponibles devront être revérifiées au démarrage du chantier.

| Composant | Déclaration actuelle / résolution locale | Cible proposée | Traitement |
| --- | --- | --- | --- |
| Frameworks | `net8.0-android`, `net8.0-windows10.0.19041.0` | `net10.0-android`, `net10.0-windows10.0.19041.0` | Conserver Android et Windows comme seules plateformes. |
| Microsoft.Maui.Controls | `8.*` / 8.0.100 | [10.0.110](https://www.nuget.org/packages/Microsoft.Maui.Controls) | Aligner paquet et workload ; figer la version. |
| Microsoft.Maui.Controls.Compatibility | `8.*` / 8.0.100 | Retrait proposé | Aucun usage explicite des layouts/renderers de compatibilité trouvé ; confirmer à la compilation. |
| CommunityToolkit.Maui | 9.0.0 | [15.0.1](https://www.nuget.org/packages/CommunityToolkit.Maui) | Cible .NET 10 ; exige MAUI Controls >= 10.0.90. Vérifier behaviors, sélecteurs et Toast. |
| CommunityToolkit.Mvvm | 8.2.0 | [8.4.2](https://www.nuget.org/packages/CommunityToolkit.Mvvm) | Conserver ObservableObject, ObservableProperty et RelayCommand. |
| Microsoft.Extensions.Logging.Debug | `8.*` / 8.0.1 | [10.0.12](https://www.nuget.org/packages/Microsoft.Extensions.Logging.Debug), ou correctif 10.0 ultérieur vérifié | Conserver la condition Debug et une version exacte. |
| sqlite-net-pcl | 1.9.172 | [1.11.285](https://www.nuget.org/packages/sqlite-net-pcl) | Mise à jour à isoler : changement de dépendances natives, détaillé ci-dessous. |
| SQLitePCLRaw.bundle_green | 2.1.10 | Suppression dans la cible sqlite-net 1.11.285, après validation | La dernière page du bundle indique 2.1.11, mais passer seulement à cette version maintiendrait l'ancienne famille 2.x. |
| OpenAI | 2.4.0 | Maintien de 2.4.0 pour la phase 1 | Adaptations minimales de compilation uniquement ; mise à jour reportée sauf incompatibilité démontrée. |
| Newtonsoft.Json | 13.0.3 | Retrait proposé ; sinon [13.0.4](https://www.nuget.org/packages/Newtonsoft.Json) | Aucun usage de Newtonsoft/JsonConvert trouvé dans les sources applicatives. |

## Changements prioritaires

### 1. Rendre la compilation reproductible — priorité P0

Fichiers : `SmallBazar.csproj`, `Directory.Packages.props`, futur `global.json`.

- Installer/vérifier les workloads .NET 10 Android et MAUI Windows ainsi que la chaîne Android compatible. Ne pas déduire leur présence du seul SDK installé.
- Ajouter un `global.json` avec une politique de correctifs explicite, documenter le SDK, le workload set, le SDK Android et le JDK validés.
- Passer les deux TFM à .NET 10 et figer `MauiVersion` et les versions de packages. Supprimer les versions flottantes `8.*`.
- Clarifier la gestion des versions : le fichier `Directory.Packages.props` existe, mais `ManagePackageVersionsCentrally` vaut `false`. On peut conserver des références explicites dans le csproj ou activer réellement la gestion centralisée ; éviter un mélange ambigu.
- Régénérer les assets NuGet avec les sources et dossiers de repli réellement disponibles. Cette étape a été effectuée après l'audit : voir le suivi du nettoyage NuGet ci-dessus.
- Revoir `CompilerGeneratedFilesOutputPath=$(SolutionDir)DummyGenerated` : rendre le chemin indépendant du mode d'ouverture solution/projet et éviter de conserver des sorties générées hors de `obj` sans nécessité.
- Exécuter les compilations et tests avec `-m:1`, conformément aux instructions du projet. Prévoir l'audit des dépendances transitives à la restauration.

### 2. Mettre à jour SQLite sans perdre les données — priorité P0

Fichiers : `Services/DataService.cs`, `Models/Box.cs`, `Models/Area.cs`, `Models/Category.cs`, `Models/BoxCat.cs`.

Le projet résout actuellement SQLitePCLRaw 2.1.10 et ses bibliothèques natives. Le `sqlite-net-pcl` 1.11.285 publié déclare désormais, pour les cibles pertinentes, `SQLitePCLRaw.core >= 3.0.3`, `SQLitePCLRaw.provider.e_sqlite3 >= 3.0.3` et `SourceGear.sqlite3 >= 3.53.3`. Il faut examiner le graphe résultant, retirer l'ancien bundle_green et valider l'initialisation du fournisseur natif ; ne pas superposer aveuglément les familles 2.x et 3.x. [Dépendances sqlite-net](https://www.nuget.org/packages/sqlite-net-pcl), [architecture SQLitePCLRaw](https://github.com/ericsink/SQLitePCL.raw).

Actions nécessaires à la fiabilité de cette migration :

- Conserver le nom `BigBazar.db3`, son emplacement logique dans AppData et les noms/tables/colonnes actuels. Un renommage « cosmétique » pourrait donner l'impression d'une perte de données.
- Introduire une initialisation asynchrone attendue et unique. Le constructeur de DataService lance quatre `CreateTableAsync` et le PRAGMA sans les attendre, puis positionne `IsInitialized=true`. Les `try/catch` autour de ces appels n'observent pas les exceptions différées.
- Sérialiser initialisation, opérations courantes et sauvegarde/restauration pour éviter l'accès à une connexion fermée ou à un fichier en remplacement.
- À la restauration : lire le fichier sélectionné par `OpenReadAsync`, copier vers un fichier temporaire, vérifier sa validité SQLite et ses tables attendues, conserver une copie de secours, puis remplacer et rouvrir la base. Toujours restaurer un état utilisable en cas d'annulation, d'extension invalide ou d'échec.
- Aujourd'hui, RestoreDatabaseAsync ferme la connexion avant le sélecteur ; certains retours, annulations ou exceptions contournent le bloc de réouverture. Une annulation peut même retourner `true` sans restauration.
- Tester sur une copie d'une ancienne base réelle, puis sur un échange de sauvegardes Android ↔ Windows. Vérifier les identifiants, catégories, emplacements, dates et numéros uniques.
- Vérifier les bibliothèques natives présentes dans l'APK/AAB Release, notamment leur compatibilité avec les appareils à pages mémoire de 16 Ko. Il s'agit d'une propriété des binaires embarqués, pas du seul code C#. [Documentation Android](https://developer.android.com/guide/practices/page-sizes).

### 3. Adapter les API et comportements MAUI/Toolkit — priorité P1

- `App.xaml.cs:13` : déplacer la création de la page racine vers `CreateWindow`, en conservant dimensions et titre Windows. `Application.MainPage` est obsolète depuis MAUI 9. Adapter aussi `Services/DialogService.cs:18` pour retrouver la page de la fenêtre concernée. [Migration MAUI 9](https://learn.microsoft.com/en-us/dotnet/maui/whats-new/dotnet-9?view=net-maui-10.0).
- `Views/SearchPage.xaml:74` : fournir explicitement le BindingContext ou la source de commande au `EventToCommandBehavior` actif du filtre par emplacement. Les behaviors du Toolkit actuel ne récupèrent pas automatiquement ce contexte. Sans adaptation, la sélection peut ne plus déclencher la recherche. Le deuxième exemple apparent du fichier appartient au filtre par catégorie commenté et n'est pas une fonctionnalité active. [Documentation du behavior](https://learn.microsoft.com/en-us/dotnet/communitytoolkit/maui/behaviors/event-to-command-behavior).
- Remplacer progressivement les `Frame` obsolètes par des `Border` dans AddBoxPage, BoxListPage, DetailPage, SearchPage, TechPage et le style commun. Reporter bordures, rayons, padding, ombres, triggers et gestes ; adapter le cast `Frame` de `Views/DetailPage.xaml.cs:17`. Il s'agit d'une modernisation recommandée, pas d'une preuve que chaque Frame empêche de compiler.
- Remplacer `DisplayAlert` par `DisplayAlertAsync` et les animations `ScaleTo`/`RotateTo` par leurs équivalents Async, notamment dans DialogService, CategoryReport, DetailPage et Animator. [Évolutions MAUI 10](https://learn.microsoft.com/en-us/dotnet/maui/whats-new/dotnet-10?view=net-maui-10.0).
- Conserver les CollectionView et le TabbedPage existants. Une migration Shell, Blazor ou une réécriture de navigation n'est pas requise pour ce périmètre.
- Conserver les `x:DataType` déjà présents ; vérifier les bindings à source explicite, templates et `ItemDisplayBinding` après mise à jour. L'activation du nouveau générateur XAML et le durcissement général du trimming peuvent rester ultérieurs.

### 4. Sécuriser l'asynchronisme au démarrage et les appels UI — priorité P1

Fichiers : `ViewModels/GlobalViewModel.cs`, `ViewModels/AreaPageViewModel.cs`, `Services/DataService.cs`, `MauiProgram.cs`.

- Le constructeur de GlobalViewModel lance un `Task.Run` non attendu et y charge l'état lié aux vues. Déplacer ce démarrage vers une méthode Task attendue par le cycle de vie, après initialisation de la base, avec mise à jour des collections et propriétés UI sur le thread principal.
- Dans BackupDatabaseAsync et RestoreDatabaseAsync, les sélecteurs, le partage et certaines alertes sont appelés à l'intérieur de `Task.Run`. Conserver les appels UI sur le thread principal ; réserver le travail en arrière-plan aux opérations qui le nécessitent. MAUI impose le thread UI pour FilePicker. [Documentation FilePicker](https://learn.microsoft.com/en-us/dotnet/maui/platform-integration/storage/file-picker?view=net-maui-10.0).
- Retirer les attentes synchrones `.Wait()` pertinentes, dont `sortList().Wait()` et la journalisation globale des exceptions, et revoir les `ConfigureAwait(false)` suivis d'interactions avec vues/navigation.
- Remplacer les méthodes métier `async void` par des Task attendues, notamment InitData d'AreaPageViewModel et les chargements/générations de CategoryReport. Les gestionnaires d'événements UI peuvent rester `async void` avec traitement d'erreur local.
- Ces fragilités préexistent ; leur présence ne prouve pas un plantage actuel. Elles augmentent le risque de régression lorsque les timings et handlers changent.

### 5. Vérifier Android avec les nouvelles cibles — priorité P1

Fichiers : `SmallBazar.csproj`, `Platforms/Android/*`, vues concernées.

- Utiliser la chaîne .NET 10 / SDK Android correspondant à API 36 et le JDK compatible validé. Distinguer API cible et version minimale installable.
- Le minimum actuel est API 21. Microsoft recommande désormais API 24 pour éviter certains problèmes Java de desugaring, tout en indiquant qu'API 21 reste supportée par .NET 10. Relever le minimum exclurait des appareils : conserver d'abord API 21 si ce parc doit être maintenu, ou décider explicitement le passage à 24 avant livraison. [Notes Android .NET 10](https://learn.microsoft.com/en-us/dotnet/maui/whats-new/dotnet-10?view=net-maui-10.0#net-for-android).
- Recetter les zones sûres, barres système, clavier, rotation et bouton Retour, en préservant le comportement des pages modales et des onglets.
- Ajouter/vérifier la déclaration Android `queries` pour `SENDTO` / `mailto` utilisée par `Email.Default.IsComposeSupported`. Elle n'apparaît pas dans le manifeste source examiné ; vérifier le manifeste fusionné. [Documentation Email](https://learn.microsoft.com/en-us/dotnet/maui/platform-integration/communication/email?view=net-maui-10.0).
- Tester sauvegarde/partage et restauration depuis un fournisseur de documents, sans supposer que `FileResult.FullPath` soit un chemin physique accessible.
- Conserver `com.enaxos.smallbazar` et la clé de signature Android utilisée pour les installations existantes ; augmenter le code de version pour une mise à jour.
- Vérifier SecureStorage lors d'une mise à jour et d'une restauration Android, puisque `allowBackup=true` est présent. Préserver le choix d'une clé fournie par l'utilisateur et gérer proprement une clé indisponible.

### 6. Réparer et rendre portable la publication Windows — priorité P1

Fichiers : `Properties/PublishProfiles/MSIX-win10-x64.pubxml`, `Platforms/Windows/Package.appxmanifest`, `SmallBazar.csproj`, `Services/VersionService.cs`.

Constats :

- Profil Windows avec `PublishDir` pointant vers `net8.0-android`.
- TFM Windows .NET 8 et RID historique `win10-x64` à revalider/remplacer par le RID portable `win-x64` pour la chaîne cible.
- Répertoire MSIX absolu sur `J:\SmallBazarSolution\publishWin\` et AppInstaller sur un partage réseau propre à la machine.
- Signature et paramètres AppInstaller Windows dans le PropertyGroup commun aux deux plateformes.
- Version applicative `1.0.0` / code `1`, version du manifeste `1.6.5.0`, service affichant artificiellement `1.6.4`.

Actions :

- Refaire le profil pour la cible Windows .NET 10, avec sorties relatives/configurables et propriétés Windows conditionnées.
- Harmoniser les versions affichée, Android et MSIX ; supprimer le contournement historique de VersionService après validation des API de version de l'application.
- Vérifier la disponibilité et la validité du certificat de signature. Ne pas changer arbitrairement Publisher, identité MSIX ou signature : comparer au paquet réellement installé avant de générer une mise à jour, car cela conditionne la continuité de l'installation et de son stockage.
- Garder initialement la version Windows minimale existante si la chaîne validée le permet ; distinguer le SDK de compilation du minimum d'exécution.
- Tester installation neuve, mise à jour en place, conservation de la base et de SecureStorage, sélecteur de dossier, sauvegarde HTML WinRT, WebView2, redimensionnement et double-clic sur les boîtes.
- Réévaluer la nécessité de `broadFileSystemAccess`, sans la supprimer avant validation des accès fichiers existants.

### 7. OpenAI — recommandations reportées après la phase 1

Fichiers : `Views/CategoryReport.xaml.cs`, `AI/AIConsts.cs`, `Utils/ApiKeyStorage.cs`, `Utils/ApiKeyValidator.cs`, `Resources/Raw/category_prompt.txt`.

Décision de périmètre : conserver ces éléments et le paquet actuel ; ne modifier que ce qui est indispensable à leur compilation sous .NET 10/MAUI 10. Les points suivants restent des observations d'audit pour une phase ultérieure et ne sont pas des critères de validation de la phase 1.

- Mettre à jour le paquet OpenAI puis vérifier la compilation de ChatClient/CompleteChatAsync et les erreurs réseau. Le modèle actuel est `gpt-4o-mini`. La mise à niveau du paquet ne justifie pas à elle seule un changement de modèle, d'API ou de prompt.
- Le parcours réalise actuellement un appel de test `ping`, puis l'appel réel ; la limite d'annulation de dix secondes ne concerne que le ping. Donner un timeout et un traitement d'erreur explicites à l'appel réel. La suppression du ping peut se faire sans changer le rapport produit, mais doit être validée dans ce parcours.
- Le parseur suppose `numéro|catégorie;catégorie` et accède directement à `Split('|')[1]`. Valider les lignes, numéros et données avant génération du HTML ; conserver la compatibilité du cache `categorizedData.txt`.
- Encoder les descriptions, emplacements et catégories insérés dans le HTML. La WebView active JavaScript pour les ancres ; des données contenant du balisage ne doivent pas devenir du code exécutable.
- Préserver la lecture du cache, la génération des ancres, l'export HTML Windows et le partage Android. Le déplacement de cette logique vers un service et un ViewModel dédiés est utile mais peut suivre la migration initiale.

## Corrections préexistantes à isoler de la migration

Ces points ont été observés dans les sources mais ne sont pas des ruptures introduites par MAUI 10. Les couvrir dans la recette, puis les corriger séparément pour identifier les changements de comportement :

- `Models/Box.cs` : AreaCode est nullable en surface mais repose sur un champ `int`, et son setter ignore `null`. Effacer réellement un emplacement peut donc ne pas se répercuter dans le modèle. Vérifier aussi le traitement historique de la valeur 0 avant de changer ce modèle.
- `DescriptionForDisplay` appelle `Description.Trim()` sans garde ; une base importée contenant NULL peut provoquer une exception.
- `ExportDataAsync` insère littéralement ` + '\t' + ` entre numéro et description ; vérifier le format réellement attendu avant correction.
- Les recherches et le rendu de rapport multiplient les chargements complets et requêtes par ligne. Des optimisations sont possibles mais non nécessaires à une première migration à comportement équivalent.
- Le GlobalViewModel regroupe plusieurs écrans et CategoryReport concentre UI, IA, cache, génération HTML et intégrations natives. Un découpage MVVM complémentaire améliorera la maintenance sans être un prérequis pour changer de framework.
- Le durcissement généralisé de nullable, une refonte graphique, NativeAOT, un changement de moteur de données ou l'ajout de fonctions restent hors du premier lot.

## Ordre de réalisation recommandé

1. Capturer la référence fonctionnelle : anciens paquets installables, identités et signatures, copie des bases et sauvegardes, captures des principaux écrans, scénario de recette.
2. Rétablir une restauration et une compilation de référence reproductibles ; documenter les limites si l'ancienne chaîne n'est plus disponible.
3. Migrer SDK/TFM, MAUI et Toolkit, corriger API et bindings, conserver les parcours et le modèle de données. Compiler les deux plateformes en Debug et Release.
4. Migrer la chaîne SQLite dans un changement identifiable, sécuriser initialisation et sauvegarde/restauration, puis valider les anciennes bases et les binaires Android.
5. Mettre à jour les autres paquets, retirer les références inutilisées, valider rapport IA, cache, HTML, réseau et SecureStorage.
6. Finaliser les paquets Android/Windows et effectuer une mise à jour en place sur des installations existantes avant toute diffusion.

Chaque étape doit rester compilable et vérifiable. Une installation qui démarre en Debug ne suffit pas à valider les bibliothèques natives, le trimming et la signature des paquets Release.

## Critères d'acceptation de l'iso-fonctionnalité

| Parcours | Vérification attendue sur Android et Windows |
| --- | --- |
| Installation et mise à jour | Installation neuve et mise à jour d'une version existante sans perte de données ni changement involontaire d'identité. |
| Boîtes | Création, numérotation unique, modification, suppression, liste, tri, sélection et précédent/suivant. |
| Emplacements et catégories | Gestion actuellement exposée par les écrans, associations et consultation conservées ; ne pas réactiver le filtre par catégorie commenté. |
| Recherche | Description avec accents/casse, emplacement, filtre et effacement du filtre ; double-clic Windows. |
| Stockage | Ancienne base, base vide, sauvegarde/restauration, annulation, fichier invalide, opérations après échec, échange Android ↔ Windows. |
| Rapport IA | Clé présente/absente/invalide, hors ligne, timeout, réponse incomplète, cache existant, ancres WebView, export/partage. Les appels réels payants se font avec autorisation. |
| Maintenance | Export texte, affichage/effacement des logs, ouverture du client mail, version affichée et liens externes. |
| Interface | Onglets, modales, Retour Android, clavier, rotation, barres système, thème existant et tailles Windows. |
| Livraison | Builds Debug/Release, APK/AAB selon distribution, MSIX signé, dépendances natives et audit NuGet transitif. |

Ajouter quelques tests ciblés sur le démarrage SQLite, la compatibilité des anciennes bases, les annulations/échecs de restauration, la recherche et le parseur de rapport. La recette native reste nécessaire pour les sélecteurs, WebView, clavier et mises à jour installées.
