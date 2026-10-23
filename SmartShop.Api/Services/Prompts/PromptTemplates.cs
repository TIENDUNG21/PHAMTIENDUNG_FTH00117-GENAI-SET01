using System.Text;
using SmartShop.Api.Models;

namespace SmartShop.Api.Services.Prompts;

/// <summary>
/// Single source of truth for every prompt sent to Gemini. Kept separate from the controllers
/// so the exact wording can be quoted verbatim in README.md.
/// </summary>
public static class PromptTemplates
{
    // ---- Requirement 1: generatedescription ----------------------------------------------

    public const string DescriptionSystemInstruction =
        "You are a professional copywriter for an online electronics store named Smart Shop. " +
        "Given a product's name, category and key features, write an engaging, accurate marketing " +
        "description in English. The description must be at most 80 words, written as flowing prose " +
        "(not bullet points), must highlight the most compelling features, and must not invent any " +
        "specification that was not provided. Do not use markdown formatting.";

    public static string BuildDescriptionUserPrompt(Product product) =>
        $"""
         Product name: {product.ProductName}
         Category: {product.Category}
         Key features: {product.Features}

         Write the product description now.
         """;

    // ---- Requirement 2: analyzereview -----------------------------------------------------

    public const string ReviewAnalysisSystemInstruction =
        "You are a review-analysis engine for an e-commerce platform called Smart Shop. Given a " +
        "customer's review text and star rating, determine the sentiment and return ONLY a JSON " +
        "object that matches the required schema. 'sentiment' must be exactly one of: Positive, " +
        "Negative, Neutral. 'score' is a number between 0.0 and 1.0 representing your confidence in " +
        "the sentiment classification. 'summary' is a concise, single-sentence summary of the review " +
        "written in English, at most 40 words. Do not include any text outside the JSON object.";

    public static string BuildReviewAnalysisUserPrompt(int rating, string content) =>
        $"""
         Customer rating: {rating}/5 stars
         Review content: "{content}"

         Analyze this review.
         """;

    public static readonly object ReviewAnalysisResponseSchema = new
    {
        type = "OBJECT",
        properties = new
        {
            sentiment = new { type = "STRING", @enum = new[] { "Positive", "Negative", "Neutral" } },
            score = new { type = "NUMBER" },
            summary = new { type = "STRING" }
        },
        required = new[] { "sentiment", "score", "summary" }
    };

    // ---- Requirement 3: shoppingassistant --------------------------------------------------

    public static string BuildAssistantSystemInstruction(IEnumerable<Product> products)
    {
        var catalog = new StringBuilder();
        foreach (var p in products)
        {
            catalog.AppendLine(
                $"- {p.ProductName} | Category: {p.Category} | Price: {p.Price:N0} VND | Features: {p.Features}");
        }

        return
            "You are the shopping assistant for Smart Shop, an online electronics store. You must " +
            "answer ONLY questions about the products listed in the CATALOG section below (availability, " +
            "specs, features, price, and comparisons between them). If the user asks anything unrelated " +
            "to these products - general knowledge, other stores, personal advice, or anything not " +
            "covered by the catalog - politely refuse and explain that you can only help with Smart " +
            "Shop's product catalog. Always answer in English, be concise, and never invent specs or " +
            "prices that are not listed in the catalog.\n\n" +
            "CATALOG:\n" + catalog;
    }
}
