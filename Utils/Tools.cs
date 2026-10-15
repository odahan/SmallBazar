using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace SmallBazar.Utils
{
    static class Tools
    {

        public static string RemoveDiacritics(string text)
        {
            // Normalisation de la chaîne en FormD pour séparer les caractères de base et les diacritiques
            var normalizedString = text.Normalize(NormalizationForm.FormD);
            var stringBuilder = new StringBuilder();

            // Parcours de la chaîne pour garder uniquement les caractères de base (lettres non accentuées)
            foreach (var c in normalizedString)
            {
                var unicodeCategory = CharUnicodeInfo.GetUnicodeCategory(c);
                if (unicodeCategory != UnicodeCategory.NonSpacingMark)
                {
                    stringBuilder.Append(c);
                }
            }

            // Retour à la forme de normalisation standard (FormC)
            return stringBuilder.ToString().Normalize(NormalizationForm.FormC);
        }

        public static string RemoveSpecialCharactersButKeepSeparators(string text)
        {
            // Expression régulière mise à jour pour ne supprimer que les caractères non alphanumériques et non séparateurs
            return Regex.Replace(text, "[^\\w\\s,;.!?]", "");
        }

        public static string CleanString(string input)
        {
            // trim spaces
            input = input.Trim();

            // Supprime les diacritiques (accents)
            string withoutDiacritics = RemoveDiacritics(input);

            // Supprime les caractères spéciaux tout en conservant les séparateurs
            string cleanedText = RemoveSpecialCharactersButKeepSeparators(withoutDiacritics);

            // Convertit en minuscules
            return cleanedText.ToLowerInvariant();
        }
    }
}
