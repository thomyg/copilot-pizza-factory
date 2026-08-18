namespace PizzaFactory.Trattoria;

/// <summary>
/// The trattoria's cast: party names, online customers, special wishes, and the reviews they
/// leave behind. All the wit lives here so the simulation code stays boring.
/// </summary>
internal static class NameBook
{
    public static readonly string[] PartyNames =
    [
        "the Rossi family", "Team Standup", "the Book Club", "Date Night", "the Bianchi cousins",
        "the Chess Club", "Marketing (again)", "the Night Shift", "Nonna & the girls",
        "the Debug Duo", "First Date (visibly)", "the Ferrari brothers", "Yoga After-Class",
        "the Quiz Team", "HR Offsite", "the Vinyl Collectors", "two Architects arguing",
    ];

    public static readonly string[] OnlineCustomers =
    [
        "Sofia L.", "Marco P.", "Deadline Dan", "Anna (the vegetarian)", "Luca B.",
        "the Startup upstairs", "Night-Owl Nadia", "Coach Kowalski", "Studio 4b",
        "Herr Doktor Huber", "Backlog Betty", "the Server Room",
    ];

    public static readonly string[] Wishes =
    [
        "extra chili oil, per favore",
        "no pineapple anywhere NEAR the table",
        "gluten-free crust if Giuseppe can swing it",
        "double mozzarella, single guilt",
        "can the Diavolo be… less diavolo?",
        "birthday at the table — candle on the pizza please",
        "asked if the tuna is 'dolphin-safe' (it is)",
        "wants the corner slice. Of a round pizza.",
        "extra napkins — there is a toddler",
        "asked to meet the pizzaiolo (Giuseppe waved)",
    ];

    public static readonly string[] HappyReviews =
    [
        "Best Diavolo this side of Naples.",
        "The crust! THE CRUST!",
        "We came for pizza, we stayed for Giuseppe's puns.",
        "Faster than my Wi-Fi and twice as reliable.",
        "The Funghi changed my stance on mushrooms. Politically.",
        "Perfetto. We're telling Nonna.",
        "Would 86 my own plans to come back.",
    ];

    public static readonly string[] NeutralReviews =
    [
        "Solid pizza. The table wobbled a little. So did I.",
        "Good, but the Hawaii discourse at table 9 got heated.",
        "Nice place. The oven upstaged the conversation.",
    ];

    public static readonly string[] GrumpyReviews =
    [
        "My Diavolo aged like fine wine. I didn't order wine.",
        "We watched three sunsets waiting. It was lunch.",
        "The pizza was great. The wait grew a beard.",
        "Lovely staff, glacial kitchen. Two stars, one for each hour.",
    ];

    public static string Pick(this string[] options, Random random) => options[random.Next(options.Length)];
}
