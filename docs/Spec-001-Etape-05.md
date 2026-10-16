# Spec-001 — Étape 05 : vues et commandes

Date : 16 octobre 2026, Europe/Paris.

## Travaux réalisés

- Le filtre par emplacement possède un Picker nommé et le EventToCommandBehavior reçoit explicitement son BindingContext. La commande reste celle du GlobalViewModel ; les éléments affichés sont typés Area.
- Les 15 Frame des pages AddBox, BoxList, Detail, Search et Tech sont remplacés par des Border. Le style nommé CardBorderStyle conserve le trait de 1, le rayon de 8, le padding implicite de 20 et les couleurs du cadre historique. Les fonds implicites sont blancs sur Android et suivent le thème sur Windows. Les couleurs locales, marges, tailles, paddings, rayon de 20 de l'icône, triggers et gestes sont reportés. Le style Border général existant reste indépendant.
- Les boutons précédent/suivant conservent leurs conditions d'affichage et de désactivation. Leur animation cible maintenant Border et utilise ScaleToAsync.
- Animator utilise RotateToAsync et ScaleToAsync, avec attente des deux animations concurrentes à chaque phase. Les suppressions d'avertissement des appels non attendus sont retirées.
- Les alertes restantes du rapport utilisent DisplayAlertAsync. L'alerte d'erreur de navigation WebView est attendue sur le thread UI. DialogService utilisait déjà les API Async depuis l'étape 04.
- La compilation des bindings avec Source est activée. Les sources explicites du SearchBar, du comportement et du double-clic sont typées ; les ItemDisplayBinding des emplacements sont typés Area. Les deux DataTemplate de boîtes et celui des emplacements conservent leurs types explicites. Le rapport déclare son type de BindingContext existant, sans changer son fonctionnement.
- Le double-clic Windows transmet la boîte de la ligne, la sélectionne explicitement et ouvre le détail. Il ignore un élément absent de la liste. Le binding du tri par description est explicitement OneWay, car sa propriété est calculée ; le bouton du tri numérique continue à piloter le tri.
- Correction ciblée révélée par la recette : ClearFilter réapplique le tri sélectionné après le rechargement. Auparavant, effacer le filtre affichait l’ordre renvoyé par SQLite alors que le bouton Text pouvait rester sélectionné.
- La liste simple reste masquée, et le filtre historique par catégorie reste commenté pour le lot 06B. Aucun changement de schéma ni appel OpenAI n'a été effectué.

## Validation de compilation

Les compilations Debug Windows non empaqueté et Android ont réussi avec zéro erreur. La vérification XAML a été forcée avec MauiStrictXamlCompilation=true et _MauiForceXamlCForDebug=true. Aucun avertissement XC de binding ni CS0618 n'apparaît dans ces journaux.

Commandes depuis la racine :

```powershell
dotnet build SmallBazar.csproj -m:1 -f net10.0-windows10.0.19041.0 -c Debug --no-restore -p:WindowsPackageType=None -p:AppxPackageSigningEnabled=false -p:GenerateAppInstallerFile=false -p:ApplicationId=com.enaxos.smallbazar.validation -p:IntermediateOutputPath=obj/windows-smoke/ -p:OutputPath=.local/windows-smoke/ -p:MauiStrictXamlCompilation=true -p:_MauiForceXamlCForDebug=true
dotnet build SmallBazar.csproj -m:1 -f net10.0-android -c Debug --no-restore -p:MauiStrictXamlCompilation=true -p:_MauiForceXamlCForDebug=true
```

Journaux locaux exclus de Git : .local/step05-windows-xaml.log, .local/step05-android-xaml.log et .local/step05-android-final.log (APK autonome final). La compilation Android a nécessité une exécution hors bac à sable pour terminer la génération du paquet.

Les avertissements restants concernent notamment SQLitePCLRaw (lot 06A), les propriétés MVVM/WinRT et la variable options inutilisée du rapport. La compilation Windows empaquetée standard échoue sur SigningCertificateThumbprintNotInStore dans cette session. La compilation non empaquetée vérifie le code et le XAML sans valider la signature ni une mise à jour MSIX.

## Recette interactive

La recette Windows reste à effectuer : le skill Computer Use a été chargé, mais son runtime échoue deux fois à l'initialisation avec « failed to prepare MXC sandbox: permission path cannot be represented losslessly ». Aucun contrôle visuel Windows de cette étape n'est donc annoncé.

Android : APK autonome (EmbedAssembliesIntoApk=true) installé avec succès sur l’émulateur dédié emulator-5580, Android 15/API 35. Une copie de sa base a été conservée avant de charger trois boîtes synthétiques : Alpha (n° 3, emplacement A), Bravo (n° 2, A), Charlie (n° 1, B). Aucun appareil personnel utilisé.

Essais effectués par ADB et lecture de l’arbre UI :
- Liste initiale : trois boîtes, ordre numérique 1/2/3 ; tri Text : Alpha/Bravo/Charlie.
- Filtre Area : les noms des deux emplacements s’affichent ; sélectionner Test Area A exécute automatiquement la commande, affiche deux éléments et Filter is active ; la liste contient Alpha et Bravo.
- Filtre Description : chercher Bravo avec la touche Entrée affiche un élément et Filter is active.
- Sélection d’Alpha : détail n° 3, description et emplacement corrects. Next affiche Bravo n° 2 ; Previous revient à Alpha. Capture du détail : flèches et cadres arrondis visibles, précédent grisé à la première position, boutons Cancel/Save désactivés sans modification.
- Modale des emplacements : deux noms visibles ; Close revient au détail.
- Modale de création : numéro proposé 4 ; Cancel revient à la liste, toujours trois boîtes.
- Effacement du filtre : les trois boîtes redeviennent visibles. La perte du tri sélectionné a été observée et corrigée dans ClearFilter. Après réinstallation de l’APK final : tri Text Alpha/Bravo/Charlie, exécution de ClearFilter conservant cet ordre après rechargement, puis retour au tri numérique 1/2/3.

Après les essais, la base de l’émulateur a été restaurée à l’identique octet par octet (integrity_check = ok), puis l’émulateur a été arrêté.

La capture synthétique .local/step05-detail.png et les scripts locaux de recette restent exclus du dépôt. Ces essais ne couvrent pas toutes les interactions : restent notamment le double-clic Windows, les commandes aux bornes pendant une modification, la comparaison complète des thèmes clair/sombre et la recette visuelle Windows. La compilation ne remplace pas cette recette.

## Références techniques

- [Comportement EventToCommandBehavior et contexte explicite](https://learn.microsoft.com/en-us/dotnet/communitytoolkit/maui/behaviors/event-to-command-behavior).
- [Bindings compilés et sources explicites](https://learn.microsoft.com/en-us/dotnet/maui/fundamentals/data-binding/compiled-bindings?view=net-maui-10.0).
- [Remplacement de Frame par Border](https://learn.microsoft.com/en-us/dotnet/maui/user-interface/controls/frame?view=net-maui-10.0).
- [Valeurs implicites de Frame dans MAUI 10.0.110](https://github.com/dotnet/maui/blob/10.0.110/src/Controls/src/Core/Frame/Frame.cs), [fond Android](https://github.com/dotnet/maui/blob/10.0.110/src/Controls/src/Core/Compatibility/Handlers/Android/FrameRenderer.cs), [fond Windows](https://github.com/dotnet/maui/blob/10.0.110/src/Controls/src/Core/Compatibility/Handlers/Windows/FrameRenderer.cs).
