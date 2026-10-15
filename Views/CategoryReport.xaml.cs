using SmallBazar.Services;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using SmallBazar.AI;
using System.Text;
using SmallBazar.Models;
using SmallBazar.Utils;
using System.Windows.Input;
using OpenAI.Chat;




#if WINDOWS
using Windows.Storage.Pickers;
using Microsoft.UI.Xaml; // Nécessaire pour accéder à la fenêtre WinUI
using WinRT.Interop;     // Nécessaire pour InitializeWithWindow
#endif


namespace SmallBazar.Views;

public partial class CategoryReport : ContentPage
{
    private readonly IAppLogger logger;
    private readonly IDialogService dialogService;
    private readonly IDataService dataService;

    // Créez une instance HttpClient singleton
    private static readonly HttpClient httpClient = new HttpClient();

    public CategoryReport(IAppLogger log, IDialogService dial, IDataService service)
    {
        InitializeComponent();

        // Associer un événement à la fin de la navigation pour détecter les erreurs de navigation si nécessaire
        ReportWebView.Navigated += ReportWebView_Navigated;

        // Activer JavaScript en accédant aux paramètres natifs de WebView une fois que le handler est prêt
        ReportWebView.HandlerChanged += WebView_HandlerChanged;

        TaskCompletionSource = new TaskCompletionSource();
        logger = log;
        dialogService = dial;
        dataService = service;
        emptyWebView();
        BindingContext = this;
        IsNotGeneratingReport = true;
        loadCachedRapport();
    }

    private void WebView_HandlerChanged(object sender, EventArgs e)
    {
        // Activer JavaScript selon la plateforme
#if ANDROID
        var platformView = ReportWebView?.Handler?.PlatformView as Android.Webkit.WebView;
        if (platformView != null)
        {
            // Activation de JavaScript sur Android
            platformView.Settings.JavaScriptEnabled = true;
        }
#elif IOS
        var platformView = webView.Handler.PlatformView as UIKit.WKWebView;
        if (platformView != null)
        {
            // Activation de JavaScript sur iOS
            platformView.Configuration.Preferences.JavaScriptEnabled = true;
        }
#endif
    }

    private bool isNotGeneratingReport;

    /// <summary>
    /// true when generating the report, false when not
    /// </summary>
    public bool IsNotGeneratingReport
    {
        get => isNotGeneratingReport;
        set { isNotGeneratingReport = value;
            MainThread.BeginInvokeOnMainThread(() => OnPropertyChanged());
        }
    }

    // Charger les données catégorisées mises en cache
    private async void loadCachedRapport()
    {
        try
        {
            // Lire les données catégorisées depuis le fichier en cache
            (string, DateTime?) cachedData0 = await ReadCategorizedDataFromFileAsync();
            var cachedData = cachedData0.Item1;

            if (!string.IsNullOrEmpty(cachedData))
            {
                var categorizedBoxes = cachedData.Split('\n'); // Diviser les données récupérées
                var htmlContent = await GenerateHtmlReport(categorizedBoxes, cachedData0.Item2 ?? DateTime.Now);  // Générer le rapport HTML

                // Afficher le contenu généré dans le WebView
                ReportWebView.Source = new HtmlWebViewSource { Html = htmlContent };
            }
            else
            {
                emptyWebView(false, "No cached data found. Refresh to create a new one.");
            }
        }
        catch (Exception ex)
        {
            // Log et gestion de l'erreur
            await logger.LogErrorAsync(AppLogger.LogLevel.Error, ex.Message);
            await DisplayAlert("Error", "Failed to load the cached report.", "OK");
        }
    }

    // Générer un nouveau rapport en demandant à l'API GPT-4
    private async void GenerateRapport()
    {

        IsNotGeneratingReport = false;

        try
        {
            // Tester la connexion Internet avant d'appeler l'API
            if (!await IsInternetOk())
            {
                await logger.LogErrorAsync(AppLogger.LogLevel.Error, "No internet connection.");
                await dialogService.ShowMessage("Error", "No internet connection. Please check your connection and try again.", "Ok");
                emptyWebView(true, "No internet connection.");
                return;
            }

            string realData = await GetRealData();  // Récupérer les données réelles simplifiées

            if (string.IsNullOrEmpty(realData))
            {
                emptyWebView(true, "No data to generate report.");
                return;
            }

            // Appel à l'API pour la catégorisation des boîtes
            var categorizedData = await CallOpenAiApiForCategoryClassification(realData);
            if (!string.IsNullOrEmpty(categorizedData))
            {
                var categorizedBoxes = categorizedData.Split('\n');  // Diviser les données récupérées

                // Sauvegarder les données GPT-4 catégorisées
                await WriteCategorizedDataToFileAsync(categorizedData);

                // Générer le rapport HTML à partir des données catégorisées
                var htmlContent = await GenerateHtmlReport(categorizedBoxes, DateTime.Now);
                ReportWebView.Source = new HtmlWebViewSource { Html = htmlContent };
            }
            else
            {
                emptyWebView(true, "No categorized data received.");
            }
        }
        catch (Exception ex)
        {
            await logger.LogErrorAsync(AppLogger.LogLevel.Error, ex.Message);
            await dialogService.ShowMessage("Report failure", $"Échec de la génération du rapport : {ex.Message}", "Ok");
            emptyWebView(true, ex.Message);
        }
        finally
        {
            IsNotGeneratingReport = true;
        }
    }

    // Vider la WebView avec un message par défaut ou d'erreur
    private void emptyWebView(bool isThisAnError = false, string reason = "")
    {
        string htmlContent = @"
            <html>
            <head>
                <style>
                    body {
                        display: flex;
                        justify-content: center;
                        align-items: center;
                        height: 100vh;
                        margin: 0;
                        text-align: center;
                        font-family: Arial, sans-serif;
                    }
                    .container {
                        margin-top: 50px;
                    }
                </style>
            </head>
            <body>
                <div class='container'>
                    " + (isThisAnError
                       ? "<h1>Error</h1><h2>See log file.</h2>"
                       : "<h1>No Report</h1><h2>Click 'Refresh' button</h2>") +
                       reason + @"<br />
                </div>
            </body>
            </html>";

        ReportWebView.Source = new HtmlWebViewSource { Html = htmlContent };
    }

    private async Task<string> CallOpenAiApiForCategoryClassification(string realData)
    {

        try
        {
            // 1) Clé API
            var key = await ApiKeyStorage.GetApiKeyAsync();
            if (string.IsNullOrEmpty(key))
            {
                await logger.LogErrorAsync(AppLogger.LogLevel.Error, "No API key found.");
                await dialogService.ShowMessage("Error", "No API key found. Please enter a valid key in the settings.", "Ok");
                emptyWebView(true, "No API key found.");
                return null;
            }

            // 2) Contrôle préventif sur la taille du prompt/données
            var estimatedTokens = EstimateTokenCount(realData);
            await logger.LogErrorAsync(AppLogger.LogLevel.Debug, $"Estimated data tokens: {estimatedTokens}");
            if (estimatedTokens > AIConsts.MaxModelTokens)
                throw new Exception($"Too many tokens. Please reduce the prompt and/or data. (current data size = {estimatedTokens})");

            // Charger le prompt depuis Resources/Raw
            var prompt = await PromptLoader.LoadAsync("category_prompt.txt");


            // 3) Prépare les messages (system + user)
            var messages = new List<ChatMessage>
            {
            new SystemChatMessage("Tu es un assistant qui analyse des boîtes de rangement et qui crée des catégories pertinentes."),
            new UserChatMessage($"{prompt}{realData}")
        };

            // 4) Options d’inférence (ne pas fixer Temperature si vous tenez au défaut du modèle)
            ChatCompletionOptions options = null;
           /* if (AIConsts.MaxTokens > 0)
            {
                options = new ChatCompletionOptions
                {
                    // Le SDK mappe automatiquement vers le bon champ REST (max_tokens / max_completion_tokens)
                    MaxOutputTokens = AIConsts.MaxTokens
                };
            }*/

            // 5) Client OpenAI officiel (modèle = ID du modèle OpenAI, ex. "gpt-4o-mini" ou "gpt-4.1-mini")
            //    NB : dans un code de prod, instanciez et réutilisez ChatClient (thread-safe) via DI/singleton. :contentReference[oaicite:1]{index=1}
            var chatClient = new ChatClient(
                model: AIConsts.Model,
                apiKey: key
            );


            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var ping = await chatClient.CompleteChatAsync(
                new[] { new UserChatMessage("ping") }, cancellationToken: cts.Token);
            await logger.LogErrorAsync(AppLogger.LogLevel.Debug, $"Ping response: {ping.ToString()}");

            // 6) Appel OpenAI
            ChatCompletion completion = await chatClient.CompleteChatAsync(messages);
                // : await chatClient.CompleteChatAsync(messages, options);

            // 7) Journalisation détaillée (finish reason + usage tokens)
            var usage = completion?.Usage;
            await logger.LogErrorAsync(AppLogger.LogLevel.Critical,
                $"Report is ok. Finish reason: {completion?.FinishReason} " +
                $"Prompt = {usage?.InputTokenCount} tokens, " +
                $"Completion = {usage?.OutputTokenCount} tokens. " +
                $"Total Tokens = {usage?.TotalTokenCount}");

            // 8) Rendu final (contenu texte de la 1re alternative)
            var text = completion?.Content?.FirstOrDefault()?.Text;
            return text;
        }
        catch (Exception ex)
        {
            await logger.LogErrorAsync(AppLogger.LogLevel.Error, ex.Message);
            emptyWebView(true, ex.Message);
            return null;
        }
    }

    // Générer le rapport HTML côté client, en récupérant également les zones de stockage
    private async Task<string> GenerateHtmlReport(string[] categorizedBoxes, DateTime dataExtractionDate)
    {
        var changeFlag = IsNotGeneratingReport == true;
        if (changeFlag) IsNotGeneratingReport = false;
        try
        {

            StringBuilder htmlBuilder = new StringBuilder();

            // Début du document HTML
            htmlBuilder.AppendLine("<!DOCTYPE html>");
            htmlBuilder.AppendLine("<html lang=\"en\">");
            htmlBuilder.AppendLine("<head>");
            htmlBuilder.AppendLine("<meta charset=\"UTF-8\">");
            htmlBuilder.AppendLine("<title>Categorized Box Report</title>");

            // Ajouter du style pour les bordures plus visibles
            htmlBuilder.AppendLine("<style>");
            htmlBuilder.AppendLine("body { font-family: Arial, sans-serif; }");
            htmlBuilder.AppendLine("table { width: 100%; border-collapse: collapse; margin-bottom: 20px; }");
            htmlBuilder.AppendLine("th, td { border: 1px solid #222; padding: 8px; text-align: left; }");
            htmlBuilder.AppendLine("th { background-color: #f2f2f2; }");
            htmlBuilder.AppendLine("a { text-decoration: none; color: blue; }");
            htmlBuilder.AppendLine("</style>");

            // Ajouter du JavaScript pour gérer les ancres
            htmlBuilder.AppendLine("<script>");
            htmlBuilder.AppendLine("function scrollToAnchor(anchor) {");
            htmlBuilder.AppendLine("  var element = document.getElementById(anchor);");
            htmlBuilder.AppendLine("  if (element) {");
            htmlBuilder.AppendLine("    element.scrollIntoView({ behavior: 'smooth' });");
            htmlBuilder.AppendLine("  }");
            htmlBuilder.AppendLine("}");
            htmlBuilder.AppendLine("</script>");

            htmlBuilder.AppendLine("</head>");
            htmlBuilder.AppendLine("<body>");

            // Titre du rapport avec la date et l'heure d'extraction
            htmlBuilder.AppendLine($"<h1>Categorized Box Report</h1><h3>Data extracted on {dataExtractionDate:MMMM dd, yyyy HH:mm:ss}</h3>");

            // Marqueur pour le sommet de la page
            htmlBuilder.AppendLine("<a id=\"top\"></a>");

            // Générer l'index des catégories, trié par ordre alphabétique

            var nonAlphanumericChars = " !\"#$%&'()*+,-./:;<=>?@[\\]^_`{|}~";
            char[] charsToTrim = nonAlphanumericChars.ToCharArray();

            var categories = categorizedBoxes
                .SelectMany(line => line.Split('|')[1].Split(';'))      // Extraire les catégories
                .Select(category => category.Trim(charsToTrim))         // Supprimer les espaces inutiles
                .Distinct(StringComparer.CurrentCultureIgnoreCase)                   // Supprimer les doublons sans tenir compte de la casse
                .OrderBy(c => c, StringComparer.CurrentCultureIgnoreCase)      // Trier de manière insensible à la casse
                .ToList();

            htmlBuilder.AppendLine($"<h2>Category Index</h2><h3>{categories.Count} entries</h3>");

            htmlBuilder.AppendLine("<ul>");
            for (var index = 0; index < categories.Count(); index++)
            {
                var category = categories[index];
                var sanitizedCategory = "CAT" + index.ToString(); // Créer un ID unique sans espaces pour les ancres
                htmlBuilder.AppendLine(
                    $"<li><a href=\"javascript:scrollToAnchor('{sanitizedCategory}')\">{category}</a></li>");
            }

            htmlBuilder.AppendLine("</ul>");

            var catIndex = 0;
            foreach (string category in categories)
            {
                var sanitizedCategory = "CAT" + catIndex.ToString();

                htmlBuilder.AppendLine($"<h2 id=\"{sanitizedCategory}\">{category}</h2>");
                htmlBuilder.AppendLine("<table>");
                htmlBuilder.AppendLine("<tr><th>Box</th><th>Description</th><th>Area</th></tr>");

                foreach (string sb in categorizedBoxes)
                {
                    if (sb.Contains(category, StringComparison.CurrentCultureIgnoreCase))
                    {
                        var boxNumber = sb.Split('|')[0].Trim();
                        var box = await dataService.GetBoxByNumberAsync(int.Parse(boxNumber));
                        var area = box != null
                            ? (box.AreaCode != null) ? await dataService.GetAreaById(box.AreaCode ?? -1) : new Area()
                            : new Area();

                        htmlBuilder.AppendLine(
                            $"<tr><td>{boxNumber}</td><td>{box?.Description}</td><td>{area?.Name ?? "Unknown"}</td></tr>");
                    }
                }

                htmlBuilder.AppendLine("</table>");
                htmlBuilder.AppendLine("<a href=\"javascript:scrollToAnchor('top')\">↑ Back to index</a>");

                catIndex++;
            }



            // Fin du document HTML
            htmlBuilder.AppendLine("</body>");
            htmlBuilder.AppendLine("</html>");

            return htmlBuilder.ToString();
        }
        finally
        {
            if (changeFlag) IsNotGeneratingReport = true;

        }
    }

    // Écrire les données catégorisées dans un fichier
    private async Task WriteCategorizedDataToFileAsync(string categorizedData)
    {
        try
        {
            string filePath = Path.Combine(FileSystem.CacheDirectory, "categorizedData.txt");
            using (StreamWriter writer = new StreamWriter(filePath, false))
            {
                await writer.WriteAsync(categorizedData);
            }
        }
        catch (Exception ex)
        {
            await logger.LogErrorAsync(AppLogger.LogLevel.Error, $"Erreur lors de l'écriture des données catégorisées : {ex.Message}");
        }
    }

    // Lire les données catégorisées à partir du fichier
    private async Task<(string, DateTime?)> ReadCategorizedDataFromFileAsync()
    {
        try
        {
            string filePath = Path.Combine(FileSystem.CacheDirectory, "categorizedData.txt");

            if (File.Exists(filePath))
            {
                FileInfo fileInfo = new FileInfo(filePath);
                DateTime lastWriteTime = fileInfo.LastWriteTime;

                using (StreamReader reader = new StreamReader(filePath))
                {
                    string categorizedData = await reader.ReadToEndAsync();
                    return (categorizedData, lastWriteTime);
                }
            }
            return (null, null);
        }
        catch (Exception ex)
        {
            await logger.LogErrorAsync(AppLogger.LogLevel.Error, $"Erreur lors de la lecture des données catégorisées : {ex.Message}");
            return (null, null);
        }
    }

    // Méthode pour tester la connexion à Internet
    private async Task<bool> IsInternetOk()
    {
        try
        {
            // Effectuer une requête GET à un site web fiable (exemple : Google)
            var response = await httpClient.GetAsync("https://www.google.com");

            // Vérifier si la requête a réussi
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            // En cas d'exception, logguer l'erreur et retourner false
            await logger.LogErrorAsync(AppLogger.LogLevel.Error, $"Internet check failed: {ex.Message}");
            return false;
        }
    }

    // Récupérer les données réelles simplifiées
    private async Task<string> GetRealData()
    {
        try
        {
            var allData = await dataService.GetBoxesAsync();
            var simplifiedData = allData.Select(box => $"{box.Number}|{box.Description}");
            return string.Join("\n", simplifiedData);
        }
        catch (Exception ex)
        {
            await logger.LogErrorAsync(AppLogger.LogLevel.Error, ex.Message);
            return Task.FromResult<string>(string.Empty).Result;
        }
    }

    // Méthode liée au bouton Refresh
    private void OnRefreshClicked(object sender, EventArgs e)
    {
        GenerateRapport(); 
    }

    // Méthode liée au bouton Close
    private void OnCloseClicked(object sender, EventArgs e)
    {
        _ = Navigation.PopModalAsync();
        TaskCompletionSource.SetResult();
    }

    public TaskCompletionSource TaskCompletionSource { get; set; }

    protected override bool OnBackButtonPressed()
    {
        return true;
    }

    // Méthode pour estimer le nombre de tokens en fonction de la longueur du texte
    private int EstimateTokenCount(string text)
    {
        int avgCharsPerToken = 4;  // En moyenne, un token correspond à environ 4 caractères
        return text.Length / avgCharsPerToken;
    }

    // Méthode pour capturer les erreurs de navigation dans le WebView
    private async void ReportWebView_Navigated(object sender, WebNavigatedEventArgs e)
    {
        if (e.Url == "about:blank")
        {
            return;
        }

        await logger.LogErrorAsync(AppLogger.LogLevel.Information, $"Navigating to: {e.Url}, Result: {e.Result}");

        if (e.Result != WebNavigationResult.Success)
        {
            var errorMessage = $"Failed to load: {e.Url}, Navigation result: {e.Result}";
            await logger.LogErrorAsync(AppLogger.LogLevel.Error, errorMessage);

            MainThread.BeginInvokeOnMainThread(() =>
            {
                DisplayAlert("Navigation Error", "Failed to load the page.", "OK");
            });
        }
    }

    // save / share report
    private async Task SaveHtmlReportAsync()
    {
        try
        {
            // Générer de nouveau le rapport HTML pour la sauvegarde/partage
            (string, DateTime?) cachedData0 = await ReadCategorizedDataFromFileAsync();
            if (string.IsNullOrEmpty(cachedData0.Item1))
            {
                await DisplayAlert("Error", "No report available for saving or sharing.", "OK");
                return;
            }

            var categorizedBoxes = cachedData0.Item1.Split('\n');
            string htmlContent = await GenerateHtmlReport(categorizedBoxes, cachedData0.Item2 ?? DateTime.Now);

            // Sauvegarde ou partage en fonction de la plateforme + data brutes dans le clipboard sous windows
#if WINDOWS
            await SaveReportWithFilePicker(htmlContent);

            await Clipboard.SetTextAsync(cachedData0.Item1);

#elif ANDROID
            await ShareReportAndroidAsync(htmlContent);
#endif
        }
        catch (Exception ex)
        {
            await logger.LogErrorAsync(AppLogger.LogLevel.Error, ex.Message);
            await DisplayAlert("Error", "Failed to save or share the report.", "OK");
        }
    }

    // Sauvegarde sous Windows en utilisant le FilePicker de MAUI
    private async Task SaveReportWithFilePicker(string htmlContent)
    {
#if WINDOWS
        try
        {
            var savePicker = new FileSavePicker();

            // Accéder à la fenêtre active de l'application MAUI
            if (Microsoft.Maui.Controls.Application.Current != null)
            {
                var elementHandler = Microsoft.Maui.Controls.Application.Current.Windows[0].Handler;
                if (elementHandler != null)
                {
                    var currentWindow = elementHandler.PlatformView as Microsoft.UI.Xaml.Window;

                    // Obtenir le handle de la fenêtre pour le picker
                    var hwnd = WindowNative.GetWindowHandle(currentWindow);

                    // Initialiser le picker avec le type de fichier et les suggestions
                    savePicker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
                    savePicker.FileTypeChoices.Add("HTML File", new List<string> { ".html" });
                    savePicker.SuggestedFileName = "Report";

                    // Attribuer le HWND à la fenêtre courante pour le picker
                    InitializeWithWindow.Initialize(savePicker, hwnd);
                }
            }

            // Lancer la boîte de dialogue pour choisir l'emplacement et le nom du fichier
            var file = await savePicker.PickSaveFileAsync();

            if (file != null)
            {
                // Ouvrir un flux d'écriture pour le fichier sélectionné
                using (var stream = await file.OpenStreamForWriteAsync())
                using (var writer = new StreamWriter(stream))
                {
                    await writer.WriteAsync(htmlContent);
                }

                await DisplayAlert("Success", "Report saved successfully.", "OK");
            }
            else
            {
                await DisplayAlert("Error", "No file was selected.", "OK");
            }
        }
        catch (Exception ex)
        {
            await logger.LogErrorAsync(AppLogger.LogLevel.Error, $"Error saving report: {ex.Message}");
            await DisplayAlert("Error", "Failed to save the report.", "OK");
        }
#else
    await Task.CompletedTask; // Ensure the method remains asynchronous        
#endif
    }


    // Partage sous Android en utilisant la fonctionnalité Share de MAUI
    private async Task ShareReportAndroidAsync(string htmlContent)
    {
#if ANDROID

        try
        {
            // Créer un fichier temporaire pour le rapport HTML
            string filePath = Path.Combine(FileSystem.CacheDirectory, "SharedReport.html");
            await File.WriteAllTextAsync(filePath, htmlContent);

            // Utiliser la fonctionnalité de partage de MAUI
            await Share.RequestAsync(new ShareFileRequest
            {
                Title = "Share Report",
                File = new ShareFile(filePath)
            });
        }
        catch (Exception ex)
        {
            await logger.LogErrorAsync(AppLogger.LogLevel.Error, $"Error sharing report: {ex.Message}");
            await DisplayAlert("Error", "Failed to share the report.", "OK");
        }
#else
        await Task.CompletedTask; // Ensure the method remains asynchronous        
#endif
    }

    // Commande liée à la sauvegarde/partage du rapport
    public ICommand SaveHtmlReportCommand => new Command(async () => await SaveHtmlReportAsync());

    // regénération du rapport
    public ICommand RegenReportCommand => new Command(async () =>
    {
        (string, DateTime?) cachedData0 = await ReadCategorizedDataFromFileAsync();
        if (string.IsNullOrEmpty(cachedData0.Item1))
        {
            await DisplayAlert("Error", "No data in the cache. Refresh to generate full report.", "OK");
            return;
        }

        var categorizedBoxes = cachedData0.Item1.Split('\n');
        string htmlContent = await GenerateHtmlReport(categorizedBoxes, cachedData0.Item2 ?? DateTime.Now);
        ReportWebView.Source = new HtmlWebViewSource { Html = htmlContent };
    });


    // Classes pour représenter la réponse JSON d'OpenAI
    public class ChatCompletionResponse
    {
        public Choice[] choices { get; set; }
        public Usage usage { get; set; }
    }

    public class Choice
    {
        public Message message { get; set; }
        public string finish_reason { get; set; }
    }

    public class Message
    {
        public string role { get; set; }
        public string content { get; set; }
    }

    public class Usage
    {
        public int prompt_tokens { get; set; }
        public int completion_tokens { get; set; }
        public int total_tokens { get; set; }
    }
}
