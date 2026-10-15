using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SmallBazar.AI
{
    /// <summary>
    /// Static class for all constants related to AI processing.
    /// </summary>
    public static class AIConsts
    {

        /// <summary>
        /// Main Prompt for the AI to generate HTML report from the given data.
        /// </summary>
        public const string Prompt = "";   //@"Voici les descriptions du contenu de boîtes de rangement. Analyse la description de chaque boîte et assigne une ou plusieurs catégories générales et pertinentes à chacune. Limite le nombre total de catégories à 60 en fusionnant les catégories similaires si nécessaire. Garde les noms de catégories aussi courts et explicites que possible, par exemple entre 1 et 3 mots. Présente les résultats dans le format suivant : 'Numéro de boîte | Catégorie(s)'. En cas de catégories multiples pour une boîte, liste-les sur la même ligne, séparées par un point-virgule ';'. Chaque boîte et ses catégories devront apparaître sur une nouvelle ligne. Sois aussi synthétique et concis que possible tout en garantissant la clarté. Voici les données à analyser : ";

        public const string Model = "gpt-4o-mini"; //"gpt-5-mini"; //  
        public const int MaxModelTokens = 100000; // 
        public const int MaxTokens = 30000; // 
        public const double Temperature = 0.2d; // avec le modèle choisit on laisse valeur par défaut et on n'utilise plus ce paramètre




    }
}
