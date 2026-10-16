# Spec-001 — Étape 07 : dépendances et compilation OpenAI

Date : 16 octobre 2026, Europe/Paris. Statut : terminée, compilations Release Android et Windows réussies.

## Périmètre réalisé

La référence directe à `Newtonsoft.Json` 13.0.3 est retirée de `SmallBazar.csproj`. La recherche dans les sources, vues, ressources et fichiers de projet ne trouve aucun usage de ses API (`Newtonsoft`, `JsonConvert`, `JObject`, `JArray`, `JToken` ou attributs de sérialisation). Le graphe NuGet restauré ne contient plus `Newtonsoft.Json` ; il conserve `OpenAI/2.4.0` et sa dépendance `System.ClientModel/1.6.1`.

Le code et les vues OpenAI compilent sans adaptation supplémentaire à .NET 10/MAUI. Aucun changement de version du paquet OpenAI n'est nécessaire.

Les éléments suivants sont conservés sans modification :

- `Views/CategoryReport.xaml` et son code : accès par `OpenAI.Chat.ChatClient`, génération, cache `categorizedData.txt`, rendu WebView, export HTML Windows et partage Android.
- `Utils/ApiKeyValidator.cs` : connexion et validation de clé via `/v1/models`.
- `Utils/ApiKeyStorage.cs` : stockage sécurisé sous la clé `user_api_key`.
- `AI/AIConsts.cs` : modèle `gpt-4o-mini` et constantes existantes.
- `Utils/PromptLoader.cs` et `Resources/Raw/category_prompt.txt` : chargement et contenu du prompt ; message système existant conservé dans le rapport.
- Commandes de connexion, suppression et vérification de clé, ainsi que l'ouverture du rapport dans `GlobalViewModel`.

## Validation et limites

| Plateforme / configuration | Résultat | Journal local |
| --- | --- | --- |
| Windows Release non empaqueté | Réussite, 0 erreur, 37 avertissements | `.local/step07-build-windows-release.log` |
| Android Release, reconstruction complète, AOT et trimming | Réussite, 0 erreur, 1 avertissement | `.local/step07-build-android-release.log` |

Les avertissements Windows concernent les champs `[ObservableProperty]` en contexte WinRT (`MVVMTK0045`), la variable `options` inutilisée du rapport (`CS0219`) et les ressources de langue (`PRI257`). Android ne signale que `CS0219`. Ces avertissements préexistants ne nécessitent aucune adaptation OpenAI pour compiler ; leur nettoyage ne fait pas partie de l'étape 07.

Les compilations incluent le code et les vues du rapport ; elles ne constituent pas une recette interactive du cache, de la WebView, des sélecteurs ou du partage.

Aucun appel distant OpenAI, aucune lecture de clé personnelle et aucun appel payant ne sont effectués. Aucune application n'est installée ou publiée dans ce lot. Les tests SQLite ne sont pas relancés : aucune modification de la couche de données n'est incluse.

La refonte OpenAI, la connexion, les améliorations réseau et de timeout, le ping, le parseur et le rendu du rapport restent reportés à une phase ultérieure. Aucune campagne de tests distante n'est ajoutée. La recette distante n'est pas une condition de clôture de cette étape ni de la phase 1.

## Commandes reproductibles

Depuis la racine du projet, avec le SDK et les workloads fixés dans `global.json` :

```powershell
dotnet build SmallBazar.csproj -m:1 -f net10.0-windows10.0.19041.0 -c Release -p:WindowsPackageType=None -p:AppxPackageSigningEnabled=false -p:GenerateAppInstallerFile=false -p:IntermediateOutputPath=obj/windows-sqlite-release/ -p:OutputPath=.local/windows-sqlite-release/

dotnet build SmallBazar.csproj -m:1 -t:Rebuild -f net10.0-android -c Release -p:ApplicationId=com.enaxos.smallbazar.sqlitevalidation
```

Ces commandes restaurent les dépendances avant compilation. L'identité Android de validation reprend celle des lots 06A/06B ; aucun changement d'identité de distribution n'est enregistré dans le projet. Windows est compilé sans empaquetage ni signature ; la publication MSIX relève de l'étape 09. Android conserve AOT et trimming, avec reconstruction complète pour garder les artefacts natifs cohérents.

Le premier essai Windows dans le bac à sable échoue à restaurer les paquets avec `NU1301` (résolution de `api.nuget.org`). La relance avec accès réseau et outils natifs autorisés réussit ; aucune modification du projet pour contourner cet échec d'environnement n'est ajoutée.
