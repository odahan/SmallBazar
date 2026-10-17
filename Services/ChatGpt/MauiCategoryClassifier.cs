using SmallBazar.Utils;

namespace SmallBazar.Services.ChatGpt;

/// <summary>Requests authentication and model selection only when an analysis actually needs them.</summary>
public sealed class MauiCategoryClassifier(ChatGptSession session, ChatGptInference inference) : ICategoryClassifier
{
    public async Task<string> ClassifyAsync(IReadOnlyList<ReportBox> boxes, CancellationToken cancellationToken)
    {
        if (!await session.HasSessionAsync())
        {
            var root = Application.Current.Windows[0].Page;
            var page = root.Navigation.ModalStack.LastOrDefault() ?? root;
            var accepted = await page.DisplayAlertAsync("ChatGPT connection", "Continue with ChatGPT to classify all boxes using your eligible plan allowance. You can review app limits in ChatGPT Settings → Usage.", "Continue with ChatGPT", "Cancel");
            if (!accepted) throw new OperationCanceledException(cancellationToken);
            await session.SignInAsync(cancellationToken);
        }
        var models = await inference.ModelsAsync(cancellationToken);
        var selectedSlug = await session.SelectedModelAsync();
        var model = models.FirstOrDefault(item => item.Slug == selectedSlug);
        if (model == null)
        {
            if (models.Count == 0) throw new ChatGptException("No models are available for this ChatGPT account.");
            var labels = models.Select(item => item.DisplayName + " (" + item.Slug + ")").ToArray();
            var root = Application.Current.Windows[0].Page;
            var page = root.Navigation.ModalStack.LastOrDefault() ?? root;
            var choice = await page.DisplayActionSheetAsync("Select a ChatGPT model", "Cancel", null, labels);
            var index = Array.IndexOf(labels, choice);
            if (index < 0) throw new OperationCanceledException(cancellationToken);
            model = models[index];
            await session.SelectModelAsync(model, cancellationToken);
        }
        cancellationToken.ThrowIfCancellationRequested();
        var prompt = await PromptLoader.LoadAsync("category_prompt.txt");
        var input = string.Join("\n", boxes.Select(box => $"{box.Number}|{box.Description}"));
        using var analysisTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        analysisTimeout.CancelAfter(TimeSpan.FromMinutes(2));
        return await inference.GenerateAsync(model,
            "Tu es un assistant qui analyse des boîtes de rangement et qui crée des catégories pertinentes.", prompt + input, analysisTimeout.Token);
    }
}
