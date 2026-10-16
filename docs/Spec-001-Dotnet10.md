# Spec-001 — Phase 1 : modernisation .NET 10

## 1. Objectif

Migrer SmallBazar vers .NET 10, .NET MAUI 10 et des dépendances stables compatibles sur Android et Windows. Conserver les fonctions utiles, avec une simplification explicitement demandée : supprimer la gestion persistée des anciennes catégories BigBazar et leur jointure avec les boîtes. Livrer une application qui démarre, retrouve les boîtes et emplacements existants et peut être mise à jour sans perte de ces données.

Date de création : 16 octobre 2026. Statut : migration en cours ; étapes 01 à 04 réalisées avec les limites de validation consignées ; étape 05 implémentée et limites documentées ; lot 06A terminé ; lot 06B implémenté et testé, recette interactive restante ; étape 07 terminée, compilation Release Android/Windows validée ; étape 08 terminée avec exception de signature autorisée ; étape 08 BIS définie, à implémenter dans une autre conversation ; étapes 09 et 10 maintenues en fin de plan. Voir le [compte rendu des étapes 01 à 04](Spec-001-Etapes-01-04.md).

Référence : [audit de modernisation](../AUDIT-MODERNISATION.md).

Périmètre révisé le 16 octobre 2026 : paquet SQLite vérifié dans la documentation Microsoft ; suppression de `Categories` et `BoxCats` prévue dans cette phase. Après le maintien de la compilation OpenAI à l'étape 07, insertion de l'étape 08 BIS : remplacer la clé API par une identification ChatGPT avec utilisation du forfait éligible, en conservant le fonctionnement du classement mémorisé et du rapport HTML. Ordre final : 08 → 08 BIS → 09 → 10 ; les vérifications encore ouvertes des étapes 05 et 06B restent à solder avant clôture.

## 2. Périmètre et règles

- Conserver les parcours boîtes, emplacements, recherche, sauvegarde/restauration, export et maintenance. Retirer les vestiges de gestion manuelle des catégories, y compris leurs références dans l'export et les données de démonstration.
- Conserver l'architecture MAUI native, le MVVM Toolkit, les onglets et les CollectionView. Adapter les services et ViewModels lorsque la migration l'exige.
- Conserver l'interface en anglais. Le gel de l'intégration OpenAI s'applique au lot 07 déjà réalisé. L'étape 08 BIS autorise le remplacement de la connexion par clé API et les adaptations de transport, de modèle et de dépendances nécessaires à l'utilisation du forfait ChatGPT. Préserver le sens du prompt, le classement multiple, les données IA mémorisées et la génération HTML locale ; toute refonte supplémentaire reste hors périmètre.
- Préserver le nom de base `BigBazar.db3`, les données et le schéma utiles de `Boxes` et `Areas`. Supprimer uniquement les tables métier historiques `Categories` et `BoxCats` par une migration versionnée, testée et précédée d'une sauvegarde. Les catégories du rapport IA et son cache ne sont pas ces anciennes tables et restent en place.
- Conserver les identités et signatures nécessaires aux mises à jour Android et Windows. Ne pas désinstaller ou réinitialiser l'application installée pour contourner un problème.
- Utiliser des versions stables et explicites. Vérifier leur compatibilité avant de les figer.
- Pour toute commande `dotnet build` ou `dotnet test`, ajouter explicitement `-m:1`.
- Chaque étape donne lieu à un changement identifiable, une validation et une mise à jour du suivi de ce document. Ne pas annoncer une plateforme validée sur la seule base d'une compilation de l'autre plateforme.
- Exécuter les essais de données sur des copies. Ne pas ajouter les bases personnelles, clés API, certificats privés ou sauvegardes au dépôt Git.

Hors périmètre : nouvelles fonctionnalités, refonte graphique, passage à Shell ou Blazor, changement de bibliothèque d'accès SQLite vers ADO.NET ou EF Core, NativeAOT, refonte générale du MVVM et optimisations sans rapport avec la migration. L'authentification ChatGPT et les adaptations réseau nécessaires au rapport entrent dans l'étape 08 BIS. La refonte générale du rapport, l'enrichissement fonctionnel de son parseur et de son rendu, la modification de sa stratégie de classement et les autres optimisations IA restent reportés. Les anomalies préexistantes sont corrigées dans des changements distincts lorsqu'elles bloquent les fonctions conservées.

## 3. État de départ

### Travaux préparatoires réalisés

- [x] Audit technique initial disponible.
- [x] Nettoyage des anciens fichiers générés référençant Syncfusion.
- [x] Configuration NuGet locale limitée à nuget.org, sans dossiers de repli hérités du poste.
- [x] Restauration des dépendances existantes réussie.
- [x] Compilation Windows .NET 8 réussie avec `-m:1` et zéro erreur.
- [x] Bases Windows localisées, sauvegardées et contrôlées avec `PRAGMA integrity_check`.

La restauration de référence a nécessité `-p:CheckEolWorkloads=false` pour l'ancienne cible Android .NET 8 sous le SDK 10. Ce paramètre n'a pas été enregistré dans le projet et ne doit pas devenir un contournement permanent après migration.

### Incident à prendre en compte

Après le nettoyage, l'utilisateur a signalé que l'application Windows installée s'ouvre sur un écran noir. Le lien causal avec le nettoyage n'est pas établi. La compilation réussie ne constitue donc pas une référence fonctionnelle validée. Cet incident doit être documenté à l'étape 01 et le démarrage doit être vérifié explicitement à l'étape 04.

### Données à préserver

Base Windows de l'application installée :

```text
C:\Users\odaha\AppData\Local\Packages\com.enaxos.smallbazar_24t49ffww27ep\LocalState\BigBazar.db3
```

Sauvegarde principale vérifiée :

```text
E:\Backups\SmallBazar\20261015T234507815478Z\SmallBazar-installed\BigBazar.db3
```

Le répertoire de sauvegarde contient également l'ancienne base BigBazar et une sauvegarde antérieure de SmallBazar. Le manifeste décrit leurs origines, dates, comptages et empreintes SHA-256 :

```text
E:\Backups\SmallBazar\20261015T234507815478Z\manifest.json
```

| Copie sauvegardée | Boîtes | Emplacements | Catégories | Associations boîte/catégorie |
| --- | ---: | ---: | ---: | ---: |
| SmallBazar installé | 72 | 11 | 37 | 18 |
| Sauvegarde antérieure SmallBazar | 71 | 11 | 37 | 18 |
| Ancienne application BigBazar | 30 | Table absente | 30 | 120 |

Ces nombres décrivent les sauvegardes réalisées, pas un état à imposer à la base active. Les sauvegardes de référence restent intactes ; créer une copie de travail pour chaque essai.

Après simplification, seuls les comptages de boîtes et d'emplacements doivent être conservés dans la base migrée. Les anciennes catégories et associations restent consultables dans les sauvegardes d'origine, mais leurs tables ne doivent plus exister dans le nouveau schéma.

## 4. Cible technique à confirmer au début du chantier

Les versions suivantes proviennent de l'audit du 16 octobre 2026. Les revérifier dans les sources officielles avant mise en œuvre et consigner les versions réellement retenues.

| Élément | Cible initiale | Étape |
| --- | --- | --- |
| SDK | .NET 10 stable, version et politique de roll-forward fixées dans `global.json` | 02 |
| Framework Android | `net10.0-android` | 03 |
| Framework Windows | `net10.0-windows10.0.19041.0` | 03 |
| Microsoft.Maui.Controls | 10.0.110, aligné avec les workloads validés | 03 |
| CommunityToolkit.Maui | 15.0.1 | 03 |
| CommunityToolkit.Mvvm | 8.4.2 | 03 |
| Microsoft.Extensions.Logging.Debug | Correctif stable 10.0 compatible, version exacte | 03 |
| sqlite-net-pcl | Paquet confirmé par Microsoft ; version stable 1.11.285 relevée, graphe natif à valider | 06A |
| SQLitePCLRaw.bundle_green | Retrait après validation de la nouvelle chaîne native du paquet retenu | 06A |
| OpenAI | 2.4.0 conservé au lot 07 ; réévaluer le SDK ou utiliser un client HTTP adapté à Responses au lot 08 BIS, avec justification et versions explicites | 07 / 08 BIS |
| Controls.Compatibility / Newtonsoft.Json | Retrait si leur absence d'utilisation est confirmée | 03 / 07 |

Les changements du socle MAUI sont traités ensemble lorsqu'ils sont nécessaires pour compiler. La modernisation SQLite est isolée pour rendre les régressions de données identifiables. Si l'ancienne chaîne SQLite bloque le démarrage en .NET 10, traiter l'étape 06 avant de valider l'étape 04 et consigner cette dépendance dans le suivi.

### Choix du paquet SQLite vérifié auprès de Microsoft

La [documentation Microsoft MAUI 10 — installation SQLite](https://learn.microsoft.com/fr-fr/dotnet/maui/data-cloud/database-sqlite?view=net-maui-10.0#install-the-sqlite-nuget-package) indique précisément `sqlite-net-pcl`, auteur **SQLite-net**, propriétaire **praeclarum**. C'est le paquet déjà utilisé par SmallBazar ; son suffixe « pcl » n'est pas un motif de remplacement pour MAUI.

Microsoft présente `Microsoft.Data.Sqlite` comme une autre solution utilisant ADO.NET. Cette alternative n'est pas retenue : elle entraînerait une réécriture inutile de la couche actuelle fondée sur `SQLiteAsyncConnection` et les attributs SQLite-net.

La [fiche NuGet de sqlite-net-pcl 1.11.285](https://www.nuget.org/packages/sqlite-net-pcl/1.11.285) déclare notamment `SQLitePCLRaw.core >= 3.0.3`, `SQLitePCLRaw.provider.e_sqlite3 >= 3.0.3` et `SourceGear.sqlite3 >= 3.53.3` pour les cibles pertinentes. Ce sont des dépendances techniques transitives du paquet choisi. Ne pas ajouter un autre paquet de nom voisin, ni conserver l'ancien bundle 2.x par habitude. Vérifier le graphe réellement restauré et les binaires Android/Windows avant de valider leur retrait ou leur initialisation. Microsoft ne fixe pas de version numérique ni de bundle supplémentaire dans le paragraphe d'installation cité.

## 5. Étapes de mise en œuvre

### Étape 01 — Établir la référence et documenter l'écran noir

Prérequis : sauvegardes disponibles. Aucun changement de framework.

- [x] Vérifier à nouveau l'existence et l'intégrité de la sauvegarde principale ; préparer une copie de travail.
- [x] Relever les versions, identités de paquet et modes de lancement : application installée, lancement depuis le projet, Debug/Release.
- [x] Collecter les logs de démarrage existants, exceptions et informations utiles à l'écran noir, sans exposer les secrets.
- [x] Distinguer le paquet installé des sorties de compilation locales. Consigner ce qui est reproduit et ce qui reste inconnu, sans attribuer la cause par supposition.
- [x] Définir la liste des parcours à conserver et relever les limites de la référence actuelle.

**Validation :** données préservées, incident décrit avec son mode de reproduction ou les limites du diagnostic, identités existantes relevées. Si l'ancienne version ne démarre pas, la migration peut avancer avec cette limite documentée ; le démarrage de la version migrée reste obligatoire avant recette.

### Étape 02 — Fixer la chaîne de développement .NET 10

Prérequis : étape 01.

- [x] Vérifier SDK, workloads Android/MAUI Windows, SDK Android, JDK et outils Windows nécessaires.
- [x] Ajouter `global.json` et documenter les versions validées, y compris le workload set retenu.
- [x] Conserver la configuration NuGet locale indépendante des dossiers de fournisseurs du poste.
- [x] Choisir une gestion des versions cohérente ; pour cette phase, conserver les références explicites dans le csproj et la propriété `MauiVersion`, sans activer partiellement la gestion centralisée.
- [x] Préparer les commandes reproductibles depuis la racine du projet, sans dépendre de chemins de sortie propres à une autre machine.

**Validation :** SDK sélectionné conforme à `global.json`, inventaire des outils enregistré et dépendances d'environnement manquantes identifiées. L'installation d'un SDK seule ne valide pas les workloads.

### Étape 03 — Migrer le socle MAUI et rétablir la compilation

Prérequis : étape 02.

- [x] Passer les deux TFM à .NET 10.
- [x] Mettre à jour MAUI Controls, CommunityToolkit.Maui, CommunityToolkit.Mvvm et Logging.Debug avec des versions exactes compatibles.
- [x] Retirer Controls.Compatibility après confirmation de l'absence d'usage.
- [x] Corriger les erreurs de compilation et de génération XAML imposées par la mise à jour, sans modifier les parcours.
- [x] Replacer les sorties de code généré sous un chemin maîtrisé, indépendant de `$(SolutionDir)`.
- [x] Régénérer les assets et examiner les dépendances transitives, avertissements de compatibilité et vulnérabilités signalées.

**Fichiers principaux :** `SmallBazar.csproj`, `Directory.Packages.props`, `MauiProgram.cs`, vues concernées par les erreurs.

**Validation :** restauration et compilations Debug Android/Windows réussies ; aucune référence Syncfusion ni version flottante conservée. Les limites de lancement restent distinctes du résultat de compilation.

### Étape 04 — Fiabiliser le démarrage et le chargement des données

Prérequis : étape 03 ; étape 06 anticipée si la chaîne SQLite historique est incompatible.

- [x] Créer la page racine dans `App.CreateWindow` en conservant le titre et les dimensions Windows.
- [x] Adapter DialogService pour retrouver la page de la fenêtre concernée.
- [x] Introduire une initialisation SQLite unique, asynchrone et attendue avant les lectures.
- [x] Sortir le chargement des données du `Task.Run` non attendu du constructeur de GlobalViewModel.
- [x] Mettre à jour l'état lié aux vues sur le thread principal et observer les erreurs de démarrage.
- [x] Retirer les attentes synchrones bloquantes pertinentes et les `ConfigureAwait(false)` incompatibles avec les suites d'opérations UI.
- [x] Vérifier l'ordre d'initialisation des services et du ServiceHelper lors de la création des fenêtres et ViewModels.
- [x] Vérifier le démarrage avec une base vide puis avec une copie de la base de 72 boîtes.

**Fichiers principaux :** `App.xaml.cs`, `MauiProgram.cs`, `Services/DataService.cs`, `Services/DialogService.cs`, `ViewModels/GlobalViewModel.cs`, `Utils/ServiceHelper.cs`.

**Validation :** les écrans s'affichent sur Android et Windows ; aucune fenêtre noire persistante ; données attendues visibles ; une erreur d'initialisation est observable et ne laisse pas un faux état « initialisé ».

### Étape 05 — Adapter les vues et commandes sans refonte graphique

Prérequis : étape 04.

- [x] Relier explicitement la commande du EventToCommandBehavior du filtre par emplacement à son contexte.
- [x] Remplacer les Frame par des Border en reportant styles, padding, rayons, triggers, gestes et animations.
- [x] Mettre à jour alertes et animations vers les API Async appropriées.
- [x] Vérifier les bindings compilés, DataTemplate, ItemDisplayBinding et sources explicites.
- [ ] Vérifier tri, sélection, précédent/suivant, double-clic Windows et navigation modale.
- [x] Ne pas activer de fonction masquée ; le filtre historique par catégorie et ses bindings seront supprimés au lot 06B. Conserver la liste simple masquée dans son état actuel.

**Fichiers principaux :** `Views/*`, `Resources/Styles.xaml`, `Utils/Animator.cs`, `Services/DialogService.cs`.

**Validation :** mêmes actions accessibles, filtres opérationnels et présentation équivalente sur les deux plateformes ; pas de refonte du thème ni des parcours.

### Étape 06 — Moderniser SQLite et simplifier le schéma

Prérequis : étape 03 et sauvegardes de référence vérifiées. Habituellement après l'étape 05 ; peut précéder l'étape 04 si nécessaire.

Réaliser deux lots distincts pour séparer les effets du changement de paquet et du changement de schéma.

#### Lot 06A — Paquet SQLite et sauvegarde/restauration

- [x] Utiliser exclusivement le paquet d'accès `sqlite-net-pcl` identifié par Microsoft (auteur SQLite-net, propriétaire praeclarum), mettre à jour sa version stable et examiner le graphe réellement résolu de ses dépendances natives.
- [x] Retirer l'ancien bundle_green si remplacé par les dépendances du paquet retenu et vérifier l'initialisation du fournisseur adapté aux deux plateformes.
- [x] Confirmer, avant simplification du schéma, la lecture des anciennes bases et la conservation des données de boîtes et d'emplacements, identifiants, dates et numéros uniques.
- [x] Sérialiser les accès concernés pendant sauvegarde, fermeture, remplacement et réouverture de la connexion.
- [x] Exécuter sélecteurs, dialogues et partage sur le thread UI.
- [x] Restaurer depuis `OpenReadAsync`, passer par un fichier temporaire, contrôler la base et conserver une copie de secours avant remplacement.
- [x] Garantir une connexion utilisable après annulation, fichier invalide ou erreur ; ne pas annoncer une restauration réussie après annulation.
- [x] Tester une base vide, la copie de 72 boîtes et la sauvegarde de 71 boîtes ; essayer la base BigBazar historique séparément et documenter sa compatibilité.
- [x] Vérifier les bibliothèques natives en Release et la compatibilité Android avec les pages mémoire de 16 Ko.
- [x] Ajouter des tests ciblés sur les parcours de données sensibles, sans embarquer de données personnelles dans les fixtures du dépôt.

**État du lot : terminé.** Recette Windows confirmée après remplacement des sélecteurs par ceux du Windows App SDK. Android Release avec AOT et trimming a passé les parcours SQLite sur les émulateurs x86_64 à pages de 4 Ko et 16 Ko. La commande de validation conserve les chemins du SDK et effectue Rebuild pour éviter les artefacts intermédiaires incohérents. Le dernier APK reconstruit démarre à froid sur l'émulateur 16 Ko, conserve les trois boîtes synthétiques et ouvre le partage Save ; les contrôles ELF et zipalign passent. Voir le [compte rendu 06A](Spec-001-Etape-06A.md).

**Validation 06A :** compilation et accès natif validés sur Android/Windows, intégrité correcte et sauvegarde/restauration opérationnelles sur copies avec le schéma historique. Le choix de paquet est consigné ; les sauvegardes initiales n'ont pas été écrasées.

#### Lot 06B — Retrait des catégories historiques BigBazar

Prérequis : lot 06A validé. Ce lot constitue le changement de schéma explicitement demandé pour la phase 1.

- [x] Inventorier tous les usages de `Category`, `BoxCat`, `Categories` et `BoxCats` ; distinguer ces modèles persistés de `CategoryReport` et des catégories générées par IA, qui sont conservés.
- [x] Retirer les modèles historiques, leurs méthodes d'IDataService/DataService, le chargement de catégories du GlobalViewModel, les propriétés/bindings inutilisés et le filtre XAML commenté.
- [x] Adapter suppression de boîte, export, réinitialisation, création des tables et génération de données de démonstration pour qu'aucun chemin n'accède aux deux tables retirées ou ne les recrée.
- [x] Définir la version du nouveau schéma et une migration unique, transactionnelle et réexécutable sans effet supplémentaire. Pour les bases historiques sans version, identifier l'état réel avant de migrer.
- [x] Avant migration d'une base utilisée, créer une sauvegarde cohérente fraîche ; conserver les sauvegardes de référence intactes. Ne pas supprimer puis recréer toute la base.
- [x] Dans la migration, supprimer la jointure `BoxCats` puis `Categories` ; enregistrer la nouvelle version seulement après succès de la transaction. En cas d'échec, ne pas déclarer l'initialisation terminée.
- [x] Préserver toutes les lignes et valeurs de `Boxes` et `Areas`. Une base neuve ne crée que ces deux tables métier, en plus des éventuelles tables internes SQLite.
- [x] Appliquer la même migration aux anciennes sauvegardes restaurées, avant leur utilisation ; accepter également les nouvelles sauvegardes déjà dépourvues de catégories.
- [x] Vérifier premier lancement, second lancement, base déjà migrée et migration interrompue/échouée. Couvrir également l'ancienne base BigBazar sans `Areas` et documenter la création d'une table d'emplacements vide si nécessaire.
- [ ] Tester sur les copies de 72 et 71 boîtes : mêmes boîtes et emplacements, tables `Categories`/`BoxCats` absentes, intégrité correcte, export et suppression de boîte fonctionnels. Vérifier l'échange des sauvegardes migrées entre Android et Windows.

**État du lot : implémenté et testé.** 25 tests natifs réussis sur données synthétiques, puis 25 sur copies des références de 72/71/30 boîtes. Sauvegarde préalable, migration transactionnelle version 1, rollback et reprise après échec vérifiés. Compilations Release Windows et Android réussies ; AOT et trimming Android conservés. La conservation des valeurs et les allers-retours SQLite sont validés ; la dernière case reste ouverte pour les parcours UI export/suppression et l'échange réel Android/Windows. Voir le [compte rendu 06B](Spec-001-Etape-06B.md).

**Fichiers principaux :** `Services/DataService.cs`, `Services/IDataService.cs`, `Models/Category.cs`, `Models/BoxCat.cs`, `ViewModels/GlobalViewModel.cs`, `Views/SearchPage.xaml`, code de migration et tests associés.

**Validation 06B :** aucun usage métier des anciens modèles/tables ; leurs noms subsistent seulement dans la migration, ses tests et la documentation. Les nouvelles bases et bases migrées contiennent les deux tables métier utiles ; boîtes et emplacements conservés ; aucun accès OpenAI ou changement de son code nécessaire à cette suppression. Les sauvegardes initiales restent disponibles pour un retour à l'ancien schéma.

### Étape 07 — Finaliser les dépendances et maintenir la compilation OpenAI

Prérequis : étapes 04 et 06.

- [x] Conserver OpenAI 2.4.0, le code d'accès, la méthode de connexion, le stockage de clé, le modèle et le prompt actuels.
- [x] Vérifier la compilation du code et des vues du rapport ; ne corriger que les incompatibilités imposées par .NET 10/MAUI et documenter ces adaptations.
- [x] Ne changer le paquet OpenAI que si une incompatibilité de compilation démontrée empêche son maintien ; retenir alors la correction minimale compatible.
- [x] Maintenir en place cache, rapport, export HTML et partage, sans refactorisation ni amélioration de leur traitement.
- [x] Retirer Newtonsoft.Json si l'absence d'usage est confirmée ; sinon appliquer un correctif stable compatible.
- [x] Consigner le report de la refonte OpenAI, de la connexion, des améliorations de timeout, du ping, du parseur et du rendu à une phase ultérieure ; ne pas construire une campagne de tests distante pour cette phase.

**Validation du lot 07 :** le code OpenAI existant compile sur les deux plateformes avec un minimum de changements ; aucun appel distant requis pour ce lot. Le report initial de la connexion est désormais levé par l'étape 08 BIS ci-dessous, dont les validations deviennent nécessaires avant la clôture finale.

**État de l'étape : terminée.** OpenAI 2.4.0 et l'intégration existante compilent sans adaptation supplémentaire. Newtonsoft.Json retiré après confirmation de son absence d'usage ; absence également vérifiée dans le graphe restauré. Compilations Release Windows non empaqueté et Android avec AOT/trimming réussies. Aucun appel distant effectué. Voir le [compte rendu 07](Spec-001-Etape-07.md).

### Étape 08 — Finaliser les adaptations Android

Prérequis : étapes 05 à 07.

- [x] Vérifier l'API cible, le SDK Android et le JDK retenus pour la chaîne .NET 10.
- [x] Conserver initialement le minimum API 21 ; si une dépendance impose de le relever ou si API 24 est retenue, documenter les appareils exclus et faire valider ce changement de compatibilité avant livraison.
- [x] Vérifier zones sûres, barres système, clavier, rotation, Retour et modales sur appareil/émulateur.
- [x] Vérifier la déclaration `queries` mailto dans le manifeste fusionné et l'ouverture du client mail.
- [x] Tester restauration depuis un fournisseur de documents et partage des sauvegardes/rapports.
- [x] Vérifier mise à jour, stockage sécurisé et comportement de la sauvegarde Android ; exclusion SecureStorage testée localement, ancienne clé abandonnée avec accord utilisateur et connexion OpenAI reportée.
- [x] Conserver l'ApplicationId Android réel et augmenter le code de version selon l’installation relevée ; exception de nouvelle signature et réinstallation explicitement autorisée après sauvegardes et contrôle de restauration.

**Validation :** APK/AAB Release installable, démarrage et principaux parcours vérifiés, compatibilité native contrôlée. L'échange de sauvegardes reste fonctionnel.

**État de l'étape : terminée avec exception de signature autorisée.** API cible 36, JDK 21.0.8, minimum 21 conservé. Zones sûres, clavier, paysage, mailto et sauvegarde sélective adaptés ; crash API 21 et contrastes du thème sombre corrigés. Release Android/Windows réussies. Recettes API 21/35/36, dont pages de 16 Ko, et smartphone API 33 réalisées. Identité Android réelle `com.companyname.smallbazar`, version 1.5.8/code 3 installée. Ancienne clé de signature irrécupérable : réinstallation autorisée après sauvegardes, restauration de la source Windows (72 boîtes/11 emplacements) et comparaison SQL intégrale réussies sur le téléphone. Mise à jour suivante code 2 → 3 avec même nouvelle signature réussie, compteur 72 conservé. Nouveau magasin de signature sauvegardé hors Git. Ancienne clé OpenAI abandonnée avec accord utilisateur ; refonte de connexion reportée. Voir le [compte rendu 08](Spec-001-Etape-08.md).

### Étape 08 BIS — Connecter AI Category Report au forfait ChatGPT

Prérequis : étape 08 réalisée, sauvegardes conservées. Étape définie après discussion avec l'utilisateur le 16 octobre 2026 ; implémentation dans une autre conversation. Elle précède la publication Windows de l'étape 09 et la recette finale de l'étape 10.

**Objectif :** supprimer l'utilisation et la saisie d'une clé API OpenAI dans SmallBazar, au profit de « Sign in with ChatGPT » et de l'autorisation d'utiliser le forfait ChatGPT éligible. Réaliser cette évolution sur Windows et Android sans changer la politique d'économie des appels ni réintroduire la gestion manuelle des anciennes catégories BigBazar.

#### Fonction du rapport et invariants

Le rapport permet de retrouver les boîtes par catégorie ; une boîte peut appartenir à plusieurs catégories. L'IA reçoit les descriptions et numéros de toutes les boîtes et renvoie un classement compact : catégories et numéros de boîtes correspondants. Le code local construit le HTML et y associe les informations nécessaires à l'affichage. L'IA ne génère pas le HTML.

Ce sont les données du dernier classement IA réussi qui sont mémorisées, avec leur horodatage, et non le fichier HTML comme source de référence. Chaque ouverture reconstruit le HTML à la volée à partir de ce classement. L'horodatage visible reste celui de l'analyse IA sauvegardée ; ouvrir, exporter ou reconstruire le HTML ne le modifie pas. Conserver les classements et dates déjà présents lorsqu'ils sont lisibles ; ne pas les effacer au changement de connexion.

| Déclencheur | Appel OpenAI | Comportement attendu |
| --- | --- | --- |
| Ouverture du rapport avec un classement mémorisé | Aucun | Reconstruire et afficher le HTML localement, avec la date de l'analyse sauvegardée. |
| Ouverture du rapport sans classement mémorisé | Une analyse complète après connexion si nécessaire | Classer toutes les boîtes, valider et mémoriser le résultat daté, puis construire le HTML. |
| Appui sur Refresh | Une analyse complète après connexion si nécessaire | Classer toutes les boîtes et remplacer le classement daté uniquement après réussite, puis reconstruire le HTML. |
| Création, modification ou suppression d'une boîte, même répétée | Aucun | Conserver le classement et sa date jusqu'au Refresh choisi par l'utilisateur. |
| Consultation, recherche dans le rapport ou export/partage HTML | Aucun | Utiliser le classement mémorisé et le traitement local existant. |

L'analyse initiale est déclenchée à l'ouverture du rapport sans données IA, jamais au simple démarrage de l'application. Les modifications successives ne provoquent ni recalcul automatique, ni invalidation du classement, ni appel différé en arrière-plan. L'utilisateur peut conserver un rapport ancien après une petite modification ou effectuer un seul Refresh après de nombreuses modifications.

La consultation d'un classement sauvegardé doit fonctionner hors ligne, déconnecté de ChatGPT ou avec une session expirée. Une annulation de connexion ou de génération, une limite de forfait, une erreur réseau, un refus du modèle ou un flux incomplet ne remplacent ni n'effacent le classement précédent et son horodatage. Si aucun classement n'existe, présenter cet état et une possibilité de réessai explicite, sans boucle d'appels. Une donnée mémorisée illisible doit produire une erreur observable sans être écrasée silencieusement.

#### Lot A — Valider la faisabilité de la connexion

- [ ] Revérifier la documentation officielle au début de l'implémentation : éligibilité du compte et de SmallBazar dans son cadre d'usage personnel local, disponibilité du service et restrictions du forfait. Ne pas promettre l'accès à tout type de compte ou de plan ; documenter séparément les conditions d'une éventuelle distribution commerciale.
- [ ] Prototyper le parcours officiel dans le navigateur système avec OAuth/OIDC et PKCE, validation du retour et autorisation effective d'utiliser le forfait, sans clé API ni secret client embarqué. La simple identification ne suffit pas à autoriser l'inférence.
- [ ] Vérifier le retour HTTP local sur `127.0.0.1` exigé par le parcours actuellement documenté. Sur Windows, vérifier l'application de développement ; sur Android, vérifier sur appareil le passage en arrière-plan pendant la connexion, la réception du retour et la reprise de l'interface, notamment sur Xiaomi. Ne pas supposer qu'un schéma URI personnalisé remplace ce retour.
- [ ] Réaliser sur chaque plateforme une requête minimale avec données synthétiques pour vérifier l'utilisation effective du forfait. Une connexion réussie ou une liste de modèles ne prouve pas à elle seule qu'une génération est autorisée. L'utilisateur saisit lui-même ses identifiants chez OpenAI.

**Jalon de faisabilité :** consigner les résultats distincts Windows/Android avant de remplacer le parcours existant. Le support Android est une hypothèse technique à valider, pas un résultat déjà acquis. Si une restriction officielle ou un échec du parcours empêche une plateforme, documenter le blocage et convenir de l'adaptation ; ne pas substituer silencieusement une clé API, un intermédiaire serveur ou des endpoints internes ChatGPT. Le parcours Windows empaqueté sera confirmé à l'étape 09.

#### Lot B — Intégrer la connexion et adapter l'appel

- [ ] Remplacer les commandes Enter Key / Check Key / Clear Key par un parcours en anglais permettant de se connecter, de voir le compte actif, de se déconnecter et d'accéder à la gestion d'usage ChatGPT. Utiliser l'action officielle Continue with ChatGPT et les indications de consommation du forfait.
- [ ] Ne demander une connexion que lorsqu'une génération en a besoin. Un compte non éligible, une autorisation refusée ou une limite atteinte donnent une explication exploitable ; aucune bascule automatique vers une facturation API séparée.
- [ ] Gérer la session, le renouvellement et la révocation. Protéger les jetons OAuth dans le stockage sécurisé local, en dehors de la base métier, du classement mémorisé, des exports, des sauvegardes transférables et des logs. La suppression de la saisie de clé API ne supprime pas le besoin de protéger les jetons. Une session expirée après une longue période sans utilisation doit permettre une reconnexion sans perte du rapport.
- [ ] Retirer l'utilisation des anciennes clés API et les commandes/services devenus inutiles après validation du nouveau parcours, sans effacer le classement mémorisé ni les données métier. Ne pas réutiliser les identifiants d'une autre application ou de Codex.
- [ ] Remplacer l'appel Chat Completions actuel par le parcours Responses officiel autorisé pour le forfait. Dans le contrat documenté à cette date : `store: false`, `stream: true`, instructions compatibles et modèle disponible pour le compte connecté. Vérifier les champs acceptés et le SDK .NET retenu ; un client HTTP adapté est possible sans dépendance à un processus Codex.
- [ ] Conserver le sens du prompt et le résultat compact. Adapter le format uniquement si nécessaire au nouveau transport et à sa validation ; documenter le choix du modèle sans figer un identifiant supposé accessible à tous les comptes.
- [ ] Effectuer une seule requête d'analyse complète par déclenchement ; supprimer le ping d'inférence préalable. Empêcher les générations concurrentes par appuis répétés, gérer annulation et durée d'attente, et ne pas relancer silencieusement une analyse après un résultat ambigu ou une interruption.
- [ ] Consommer le flux jusqu'à sa réussite complète ; vérifier le format et les références de boîtes avant publication. Mémoriser de manière atomique le nouveau classement et son horodatage uniquement après validation, puis reconstruire le HTML. Un résultat partiel ne devient jamais le dernier rapport réussi.
- [ ] Conserver le rendu HTML déterministe, sa recherche, l'export/partage et la compatibilité des classements existants. Ne pas réintroduire `Categories`/`BoxCats` ni modifier `Boxes`/`Areas` pour cette connexion. Garder l'interface utilisable dans les thèmes clair et sombre.

#### Recette et critères de clôture

- [ ] Tester avec un service simulé le nombre d'appels : ouvertures répétées avec classement = 0 ; modifications de boîtes = 0 ; première ouverture sans classement = 1 ; chaque Refresh accepté = 1, sans ping supplémentaire. Le renouvellement OAuth n'est pas une requête d'analyse.
- [ ] Tester la reconstruction HTML, la conservation de la date d'analyse, la lecture du classement antérieur, le redémarrage et la consultation hors ligne ou déconnectée.
- [ ] Tester les échecs de connexion, refus de permission, expiration de session, quota, annulation, flux interrompu et réponse invalide ; vérifier la conservation du dernier classement et de sa date, ainsi que la possibilité de réessai explicite.
- [ ] Vérifier réellement connexion, analyse synthétique, mémorisation et réouverture sans nouvelle analyse sur Windows et Android. Limiter les appels distants de recette ; aucun envoi de descriptions personnelles n'est nécessaire à ces essais.
- [ ] Compiler les deux cibles avec `-m:1`, vérifier Release Android avec AOT/trimming et documenter versions, commandes, résultats et limites dans un compte rendu dédié. Conserver sauvegardes, identités et nouvelle signature Android déjà validées.

**Validation :** génération sans clé API avec un forfait autorisé sur les deux plateformes ; classement multiple compact conservé ; consultation locale sans appel ; Refresh et première ouverture sans classement sont les seuls déclencheurs d'analyse ; horodatage fidèle à la dernière analyse réussie ; dernier classement protégé contre les échecs. La compilation seule ne clôture pas cette étape. La recette finale de l'étape 10 inclut ces parcours et l'étape 09 vérifie leur fonctionnement dans le paquet Windows installé.

**État de l'étape : spécifiée, à implémenter.** Aucun prototype, appel distant ni changement de code n'est réalisé lors de l'insertion de cette spécification. Les améliorations générales du rapport et du système de catégories restent hors de ce lot.

**Fichiers à examiner :** `Views/CategoryReport.xaml` et `.xaml.cs`, `Views/TechPage.xaml`, `ViewModels/GlobalViewModel.cs`, `Utils/ApiKeyStorage.cs`, `Utils/ApiKeyValidator.cs`, `AI/AIConsts.cs`, `Resources/Raw/category_prompt.txt`, services d'authentification et de génération à isoler avec MVVM Toolkit et injection de dépendances, configuration des plateformes et dépendances nécessaires. Vérifier les chemins réels avant modification.

**Références officielles vérifiées pendant le cadrage, à relire avant implémentation :**

- [Disponibilité et permission d'utiliser le forfait ChatGPT](https://developers.openai.com/siwc/quickstart).
- [Présentation pour projets open source et personnels locaux](https://developers.openai.com/cookbook/articles/sign-in-with-chatgpt).
- [Inscription, connexion et retour local](https://developers.openai.com/siwc/token-sharing-open-source/sign-in).
- [Comptes et sessions](https://developers.openai.com/siwc/token-sharing-open-source/profiles-and-sessions).
- [Modèles et requêtes Responses](https://developers.openai.com/siwc/token-sharing-open-source/models-and-inference).
- [Restrictions actuelles du parcours](https://developers.openai.com/siwc/token-sharing-open-source/preview-limitations).

### Étape 09 — Remettre à plat la publication Windows

Prérequis : étapes 05 à 08 BIS ; conserver cette étape après la modification AI Category Report.

- [ ] Corriger le profil MSIX : TFM .NET 10, RID portable validé et répertoires de sortie cohérents.
- [ ] Retirer les chemins de sortie absolus hérités ; rendre l'adresse AppInstaller configurable selon le mode de distribution retenu.
- [ ] Conditionner les propriétés de signature et de publication Windows pour ne pas affecter Android.
- [ ] Harmoniser version affichée, version MSIX et version du projet ; remplacer le contournement de VersionService.
- [ ] Vérifier certificat, Publisher et identité contre l'installation existante avant de produire le paquet de mise à jour.
- [ ] Conserver le minimum Windows actuel sauf contrainte vérifiée et décision documentée.
- [ ] Tester sélecteurs, WebView2, sauvegarde HTML, client mail, redimensionnement et double-clic.
- [ ] Vérifier une installation neuve dans un environnement séparé, puis préparer le paquet destiné à la mise à jour en place.
- [ ] Confirmer la connexion ChatGPT et le retour du navigateur dans le paquet Windows installé ; vérifier génération et consultation du classement mémorisé sans clé API.

**Validation :** MSIX signé généré, paramètres reproductibles et identité de mise à jour confirmée. Toute installation de validation qui remplace la version personnelle doit intervenir après sauvegarde fraîche et préparation du retour arrière.

### Étape 10 — Recette complète et clôture de la phase 1

Prérequis : étapes 08, 08 BIS et 09 ; points de validation antérieurs, notamment étapes 05 et 06B, soldés. Cette étape reste la dernière du plan.

- [ ] Compiler les deux plateformes en Debug et Release et traiter les avertissements introduits par la migration.
- [ ] Exécuter les tests ciblés et l'audit NuGet, puis consigner les éventuels problèmes restants.
- [ ] Recetter création/modification/suppression de boîtes, numérotation, tri, recherche et emplacements ; vérifier l'absence des anciennes catégories persistées et de leurs accès.
- [ ] Recetter sauvegarde, restauration, annulations, fichiers invalides, export, logs et liens externes.
- [ ] Recetter AI Category Report issu de 08 BIS sur les deux plateformes : connexion au forfait, première analyse, Refresh, conservation du classement et de sa date après échec, puis consultation et export sans nouvelle analyse. Confirmer que les modifications de boîtes ne déclenchent aucun appel ; la refonte générale du rapport reste hors périmètre.
- [ ] Avant une mise à jour sur un appareil utilisé, refaire une sauvegarde cohérente et relever l'état des données.
- [ ] Vérifier la mise à jour en place sur Windows et Android, la conservation des données et des clés lorsque leur stockage le permet.
- [ ] Vérifier de nouveau l'absence d'écran noir et le fonctionnement de l'application installée, indépendamment du lancement depuis l'IDE.
- [ ] Consigner les versions finales, commandes, résultats de recette et limites connues dans ce document ou un compte rendu lié.

**Validation finale :** application .NET 10 opérationnelle sur les deux plateformes, fonctions du périmètre conservées, suppression contrôlée de `Categories`/`BoxCats`, boîtes et emplacements vérifiés après mise à jour. L'intégration au forfait ChatGPT et le fonctionnement économe du rapport définis à l'étape 08 BIS sont validés sur les deux plateformes ; les appels de recette restent limités et utilisent des données synthétiques. Les autres points non testés restent ouverts.

## 6. Commandes de validation de référence

À lancer depuis la racine SmallBazar, après passage aux TFM .NET 10 et installation des outils. Ces commandes sont des modèles de compilation ; les paramètres de signature/publication seront définis aux étapes 08 et 09.

```powershell
dotnet restore SmallBazar.csproj
dotnet build SmallBazar.csproj -m:1 -f net10.0-windows10.0.19041.0 -c Debug --no-restore
dotnet build SmallBazar.csproj -m:1 -f net10.0-android -c Debug --no-restore
dotnet build SmallBazar.csproj -m:1 -f net10.0-windows10.0.19041.0 -c Release --no-restore
dotnet build SmallBazar.csproj -m:1 -f net10.0-android -c Release --no-restore
```

Lorsqu'un projet de tests aura été ajouté, exécuter `dotnet test` avec son chemin explicite et `-m:1`. Ne pas remplacer la recette des intégrations natives par les seuls tests unitaires.

## 7. Retour arrière et gestion des blocages

- Conserver un état Git identifiable avant chaque étape et limiter chaque changement au lot annoncé.
- Lorsqu'une étape échoue, relever commande, configuration, plateforme et message d'erreur ; corriger le lot concerné avant de poursuivre les validations qui en dépendent.
- Revenir au code précédent n'implique pas de restaurer automatiquement une base : vérifier d'abord la compatibilité du schéma et ne jamais remplacer des données récentes par une ancienne sauvegarde sans décision explicite.
- Après suppression de `Categories` et `BoxCats`, un retour à l'ancien binaire doit être préparé à partir d'une sauvegarde d'avant migration ; ne pas considérer le nouveau schéma comme compatible avec l'ancien code. Préserver séparément toute donnée créée depuis la migration avant un retour arrière.
- Conserver les anciens paquets installables disponibles, les informations de signature et les sauvegardes hors du dépôt. Leur disponibilité est à vérifier avant l'essai de mise à jour sur les installations personnelles.
- Un blocage de signature ou d'appareil doit être documenté. Le travail indépendant peut continuer, mais la livraison et la validation de la plateforme restent ouvertes.

## 8. Suivi d'avancement

Mettre à jour le statut et renseigner la preuve après chaque étape : commande et résultat, compte rendu de recette ou référence de changement. Les cases des travaux préparatoires ne valent pas validation de la migration.

| Étape | Statut | Preuve / résultat |
| --- | --- | --- |
| 01 — Référence et écran noir | Réalisée, limites documentées | Trois sauvegardes revérifiées et copiées ; identité 1.6.5.0 et événements MoAppHangXProc relevés. Ancien lancement installé non reproduit. [Compte rendu](Spec-001-Etapes-01-04.md). |
| 02 — Chaîne .NET 10 | Réalisée | global.json : SDK et workload set 10.0.401 ; outils Android/JDK et Windows inventoriés, commandes reproductibles documentées. |
| 03 — Socle et compilation | Réalisée | Restauration et Debug Android/Windows réussis, zéro erreur. MAUI 10.0.110, Toolkit.Maui 15.0.1, MVVM 8.4.2 ; avertissements consignés. |
| 04 — Démarrage et données | Réalisée, limites documentées | Windows non empaqueté et émulateur Android 15 : base vide et 72 boîtes visibles, sans écran noir. Erreur Windows visible et reprise vérifiée. 5 tests SQLite réussis sur copies ; pas de recette de mise à jour installée. |
| 05 — Vues et commandes | Implémentée, recette interactive à compléter | Border, contexte explicite du filtre, bindings compilés et API Async adaptés. Compilations Debug Android et Windows non empaqueté réussies ; filtres, tri, sélection, précédent/suivant et modales essayés sur émulateur Android avec données synthétiques. Recette Windows restante. Voir le [compte rendu](Spec-001-Etape-05.md). |
| 06A — Paquet SQLite et sauvegardes | Terminé | sqlite-net-pcl 1.11.285 ; 21 tests natifs réussis ; recettes Android 4/16 Ko et Windows confirmées. Dernier APK entièrement reconstruit : démarrage à froid, trois boîtes conservées, Save et alignement natif vérifiés. [Compte rendu](Spec-001-Etape-06A.md). |
| 06B — Suppression des anciennes catégories | Implémenté, recette interactive à compléter | Migration transactionnelle version 1 avec instantané préalable ; 25 tests natifs synthétiques puis sur copies 72/71/30 réussis. Release Android/Windows compilées. Export/suppression UI et échange interplateforme restants. [Compte rendu](Spec-001-Etape-06B.md). |
| 07 — Dépendances et compilation OpenAI | Terminée | Newtonsoft.Json inutilisé retiré ; OpenAI 2.4.0, accès, clé, modèle, prompt et rapport conservés sans adaptation. Release Android (AOT/trimming) et Windows non empaqueté : zéro erreur. Refonte et recette distante reportées. [Compte rendu](Spec-001-Etape-07.md). |
| 08 — Android | Terminée, exception de signature autorisée | API 21/35/36 et smartphone API 33 vérifiés, alignement 16 Ko contrôlé. 1.5.8/code 3 installé, thème sombre corrigé. Source Windows 72 boîtes/11 emplacements restaurée et comparée intégralement ; nouvelle signature sauvegardée hors Git après accord. Connexion OpenAI reportée. [Compte rendu](Spec-001-Etape-08.md). |
| 08 BIS — AI Category Report / connexion ChatGPT | Spécifiée, à implémenter | Remplacer la clé API par le forfait éligible ; classement daté mémorisé, HTML local, analyse uniquement sans classement ou sur Refresh. Prototype Android à valider ; réalisation dans une autre conversation. |
| 09 — Publication Windows | À faire après 08 BIS | Publication et recette du paquet Windows incluant la nouvelle connexion. |
| 10 — Recette et clôture | À faire en dernier | Solder les vérifications antérieures et recetter les deux plateformes, y compris 08 BIS. |
