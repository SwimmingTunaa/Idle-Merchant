using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The fixed cast of walk-ins: a name + epithet per *look* (the override controller's name, so a Villager Woman look
/// never gets a Bartholomew) and a blurb per *def* (the archetype's voice; "{want}" = the item they're after).
/// Rolled per visit in CustomerAgent.Init, never saved — the same faces recur on their own, which is the point.
/// ponytail: static tables, no memory; a regulars roster (visits, snubs) would be a SaveData chunk keyed on name.
/// </summary>
public static class CustomerCast
{
    public readonly struct Persona
    {
        public readonly string Name, Epithet, Blurb;
        public Persona(string name, string epithet, string blurb) { Name = name; Epithet = epithet; Blurb = blurb; }
        public string Title => string.IsNullOrEmpty(Epithet) ? Name : $"{Name}, {Epithet}";
    }

    // ───────────────────────── names + epithets, keyed by look (override controller name) ─────────────────────────
    private static readonly Dictionary<string, (string, string)[]> Names = new()
    {
        ["Villager Man"] = new[]
        {
            ("Bartholomew Pease", "turnip man"), ("Osric Bumble", "of the leaky roof"), ("Wendel Hob", "who owes everyone"),
            ("Tam Fiddlewick", "part-time fiddler"), ("Cuthbert Mossy", "mossy on one side"), ("Alf Pudding", "the fourth"),
            ("Godfrey Teasel", "goat-adjacent"), ("Pip Harrow", "still growing"),
        },
        ["Villager Woman"] = new[]
        {
            ("Mabel Turnipwhistle", "of dubious repute"), ("Agnes Thimble", "sharp with a needle"), ("Hilda Crumb", "pie baroness"),
            ("Dorrit Fennel", "keeper of nine hens"), ("Prudence Bramble", "not that prudent"), ("Nell Sorrel", "knows what you did"),
            ("Winifred Gooseberry", "retired from gossip"), ("Tilly Marrow", "allotment queen"),
        },
        ["Peasant"] = new[]
        {
            ("Mud", "just Mud"), ("Lumpy Jack", "barefoot by choice"), ("Odd Toby", "smells of hay"),
            ("Nettle", "found in a ditch"), ("Scrump", "apple thief, reformed"), ("Dib Clod", "clod of the Clods"),
            ("Half-Pint Hal", "full-pint drinker"), ("Weevil", "don't ask"),
        },
        ["Potion Lady"] = new[]
        {
            ("Granny Wort", "licensed to brew"), ("Old Mother Gristle", "three warts, all lucky"), ("Hepzibah Moss", "allergic to cats"),
            ("Dame Ivy Nightshade", "mostly harmless"), ("Auntie Toad", "a title, not a species"), ("Marjoram Quill", "of the crooked kettle"),
            ("Widow Bramblethorn", "fourth husband pending"), ("Grimalda Fen", "speaks to mushrooms"),
        },
        ["Old Man"] = new[]
        {
            ("Grandpa Nettles", "knows a remedy"), ("Old Ferris", "was a soldier, allegedly"), ("Elder Thistlewood", "village memory"),
            ("Doddering Gus", "doddering with purpose"), ("Barnaby Rue", "retired apothecary"), ("Old Man Crumb", "Hilda's father-in-law"),
            ("Wizened Pol", "sees badly, hears everything"), ("Grumbling Ned", "it was better before"),
        },
        ["Dwarf Miner"] = new[]
        {
            ("Brokk Deepshovel", "wholesale"), ("Dunna Orefist", "buys by the barrow"), ("Gimbal Rockwaller", "pays in exact change"),
            ("Thorva Pickbright", "the haggler"), ("Oskar Flintbeard", "mind the beard"), ("Bodil Cartwell", "wagon's outside"),
        },
        ["Dwarf Hunter"] = new[]
        {
            ("Hrolf Tusker", "trades in bulk"), ("Yngvi Boarsbane", "smells of the hunt"), ("Dagny Trapline", "counts twice"),
            ("Ketil Furrow", "buys for the whole hold"), ("Signe Longbolt", "never misses a bargain"), ("Grettir Pelt", "will take the lot"),
        },
        ["Swordsman"] = new[]
        {
            ("Sir Roderick Blunt", "third of his name"), ("Dash Everhart", "hero of somewhere"), ("Bram Steelsong", "sings badly"),
            ("Hector Stout", "lost the last duel"), ("Valen Crowe", "has a theme tune"), ("Pike Marlow", "wrong weapon, right attitude"),
        },
        ["Archer"] = new[]
        {
            ("Fletcher Quickquill", "never missed, ask him"), ("Rowan Thistle", "one eye, sharp"), ("Wren Ashby", "shoots first"),
            ("Corwin Longstride", "walks everywhere"), ("Ilse Fernshot", "bowstring-cold"), ("Perrin Dusk", "out past curfew"),
        },
        ["Spearman"] = new[]
        {
            ("Ulric Longreach", "keeps his distance"), ("Jonas Pikestaff", "very tall, very polite"), ("Dirk Halloway", "points at things"),
            ("Brannoc Tusk", "boar-poker"), ("Leon Fairweather", "only fights on sunny days"), ("Hob Lancer", "freelance"),
        },
        ["Crossbowman"] = new[]
        {
            ("Gunter Windlass", "slow to reload, quick to brag"), ("Isolde Bolt", "the punctual"), ("Matthias Crank", "mechanically minded"),
            ("Kaspar Quarrel", "picks them"), ("Berta Latch", "pays up front"), ("Rufus Trigg", "one shot, one tavern story"),
        },
        ["Elf Swordsman"] = new[]
        {
            ("Faelar Moonblade", "the eternally bored"), ("Aerith Silverleaf", "centuries of taste"), ("Thalion Vane", "sighs at humans"),
            ("Lirael Dawnedge", "sharper than she looks"), ("Caladir Wist", "has read your diary"), ("Elowen Glade", "elegantly late"),
        },
        ["Elf Spearman"] = new[]
        {
            ("Galadar Thornspire", "patient as a tree"), ("Nimue Starlance", "aims for the stars"), ("Arvandor Yew", "older than the shop"),
            ("Sylwen Reed", "quiet, terrifying"), ("Ithil Vale", "moonlit"), ("Taurien Brook", "waited a century for this"),
        },
        ["Dwarf Warrior"] = new[]
        {
            ("Thorgrim Axehollow", "the unimpressed"), ("Durgan Ironbrow", "hates stairs"), ("Helga Stonefist", "arm-wrestle champion"),
            ("Borri Emberbeard", "beard still smouldering"), ("Magni Kegsplitter", "ask about the keg"), ("Vigdis Oathhammer", "swore off ale, briefly"),
        },
        ["Dwarf Rifleman"] = new[]
        {
            ("Fenwick Powderkeg", "lightly singed"), ("Bruni Flashpan", "eyebrows pending"), ("Orla Muzzle", "loud, apologetic"),
            ("Ragnar Boomstick", "named it himself"), ("Sten Ramrod", "stands very straight"), ("Hilde Flintlock", "aims, eventually"),
        },
        ["Elf Mage"] = new[]
        {
            ("Vexalia Moonwhisper", "collector of oddities"), ("Orinthal Dusk", "owns too many relics"), ("Seraphine Glim", "curates, never uses"),
            ("Aldric Hollowmere", "pays in riddles and gold"), ("Ysolde Verrin", "sniffs the merchandise"), ("Quillon Fade", "appraises you too"),
        },
        ["Elf Arcane Archer"] = new[]
        {
            ("Nyx Emberquiver", "hoards the strange"), ("Teodric Vellum", "catalogues everything"), ("Maelis Starcatch", "wants the rarest"),
            ("Cirion Lune", "has a display case"), ("Avelune Sorrow", "collects for a museum, allegedly"), ("Ravik Glass", "touches with gloves"),
        },
        ["Noble Man"] = new[]
        {
            ("Lord Percival Ashworth-Finch", "of the lesser Finches"), ("Baron Hugo Vellacourt", "recently baron"), ("Sir Aldous Pemberly", "thrice-knighted, once deserved"),
            ("Count Lucian Marrowgate", "counts everything"), ("Earl Fitzwilliam Crane", "allergic to peasants"), ("Viscount Tobias Lark", "very new money"),
        },
        ["Noble Woman"] = new[]
        {
            ("Lady Honoria Sweetbriar", "of the Sweetbriar Sweetbriars"), ("Duchess Elspeth Carrow", "a duchess, in her way"), ("Dame Cressida Thorne", "dangerously bored"),
            ("Baroness Ottoline Vale", "wears the estate"), ("Countess Marguerite Lune", "collects husbands"), ("Lady Petronella Gilt", "all of it gilt"),
        },
        ["King"] = new[] { ("King Aldric the Mostly Wise", ""), ("King Bertram the Sufficient", ""), ("King Osmund the Unhurried", "") },
        ["Queen"] = new[] { ("Queen Isadora the Exacting", ""), ("Queen Margery the Serene", ""), ("Queen Helewise the Unamused", "") },
        ["Prince"] = new[] { ("Prince Cedric", "heir, probably"), ("Prince Rupert", "the spare"), ("Prince Lysander", "between quests") },
        ["Princess"] = new[] { ("Princess Amaranth", "unbetrothed, by choice"), ("Princess Rosalind", "escaped her lessons"), ("Princess Viola", "has her own sword") },
        ["Dwarf King"] = new[] { ("Thane Brakkar Stonecrown", "under the mountain"), ("High King Dorin Deepvault", "the thrice-bearded") },
        ["Dwarf Bear Rider"] = new[] { ("Ulfric Bearback", "the bear's name is Clive"), ("Hildrun Clawrider", "bear outranks her") },
        ["Dwarf Shieldbreaker"] = new[] { ("Grondar Wallsplitter", "breaks shields, buys shields"), ("Thrain Bulwark", "walked through the door, literally") },
        ["Elf Queen"] = new[] { ("Queen Elenwë of the Silver Court", ""), ("Queen Ithilwen the Unending", "") },
        ["Elf Pegasus Rider"] = new[] { ("Aerendyl Skyhoof", "pegasus waiting outside"), ("Lúthien Cloudmane", "arrived by air") },
        ["Elf Warden"] = new[] { ("Warden Caelthas", "of the outer wood"), ("Warden Mirelle", "keeps the queen's receipts") },
    };

    private static readonly (string, string)[] Anyone =
    {
        ("A Stranger", "not from round here"), ("Someone's Cousin", "visiting"), ("The Quiet One", "says nothing, buys plenty"),
    };

    // ───────────────────────── blurbs, keyed by def name (the archetype's voice) ─────────────────────────
    private static readonly Dictionary<string, string[]> Blurbs = new()
    {
        ["Villager Man"] = new[]
        {
            "Came in for {want}. Will leave with {want} and three opinions about the weather.",
            "Swears the last {want} from here was better. Buying it again anyway.",
            "Has been saving for {want} since the harvest. The one before last.",
            "Only popped in to get out of the rain. Now wants {want}.",
            "Heard a rumour about {want}. Doesn't remember what it was.",
            "Walked all the way from the far field. Wants {want} and a sit down.",
        },
        ["Potion Lady"] = new[]
        {
            "Needs {want} for a brew. Refuses to say what it cures.",
            "Says {want} is overpriced, then buys it, then says it again.",
            "Claims {want} will grow hair back. Hers is doing fine.",
            "Keeps a tally of every {want} ever sold here. In her head. Accurately.",
            "The kettle wants {want}. The kettle has opinions.",
            "Buying {want} for a nephew. There is no nephew.",
        },
        ["Bulk Trader"] = new[]
        {
            "Buys {want} by the barrow and complains about the weight.",
            "Wants the lot. Every {want} on the shelf, and the shelf if it's going.",
            "Does the sums on his fingers. Runs out of fingers. Buys {want} anyway.",
            "Has a cart outside, a hold to feed, and no patience for small talk. {want}, please.",
            "Trades {want} three towns over for double. Doesn't think you know that.",
        },
        ["Adventurer Customer"] = new[]
        {
            "The last {want} broke on a rat. The rat was very large, honest.",
            "Needs {want} before the quest. Hasn't been told what the quest is.",
            "Tests every {want} by swinging it near the shelves. Please don't.",
            "Heard your {want} is the best. Heard the same thing about four other shops.",
            "Dungeon at dawn. {want} by dusk. Sleep, apparently, never.",
            "Wants {want} that matches the cloak. The cloak is terrible.",
            "Owes a healer, a smith and a bard. {want} first, debts after.",
        },
        ["Noble"] = new[]
        {
            "Doesn't need {want}. Needs to be seen buying {want}.",
            "Will pay for {want}, but would prefer to be fawned over first.",
            "Has three of these at the manor. Lacks a fourth, which is unthinkable.",
            "Wants {want} wrapped, delivered, and ideally admired on the way out.",
            "Heard the king has {want}. That settles it.",
        },
        ["Collector"] = new[]
        {
            "Doesn't use {want}. Keeps it in a cabinet and visits it on Sundays.",
            "Has been hunting {want} across six provinces. You had it the whole time.",
            "Will pay double for {want} and triple if you tell its story.",
            "Appraises {want} with a tiny glass. Then appraises you.",
            "The cabinet has a gap the exact shape of {want}. It keeps them up at night.",
        },
        ["Royalty"] = new[]
        {
            "A royal visit. The {want} had better be the finest in the realm.",
            "The court requires {want}. The court does not explain itself.",
            "Came incognito. The crown gave it away.",
            "Will take {want}, and the shopkeeper's name, for the chronicles.",
        },
    };

    private static readonly string[] AnyBlurb =
    {
        "Here for {want}. Not much of a talker.",
        "Came in, looked around, wants {want}. Simple as that.",
    };

    /// <summary>Roll a persona for a spawn: name by look, blurb by def (royal defs share the Royalty voice).</summary>
    public static Persona Roll(CustomerDef def, string look)
    {
        var names = look != null && Names.TryGetValue(look, out var n) ? n : Anyone;
        var (name, epithet) = names[Random.Range(0, names.Length)];

        string key = def != null && Blurbs.ContainsKey(def.name) ? def.name
                   : def != null && def.customerArcheType == CustomerArcheType.Royalty ? "Royalty" : null;
        var blurbs = key != null ? Blurbs[key] : AnyBlurb;
        return new Persona(name, epithet, blurbs[Random.Range(0, blurbs.Length)]);
    }

    // What "{want}" reads as while they're still browsing: the archetype's kind of thing, so the blurb scans
    private static readonly Dictionary<string, string> Browsing = new()
    {
        ["Villager Man"] = "something or other", ["Potion Lady"] = "an ingredient", ["Bulk Trader"] = "ore", ["Adventurer Customer"] = "a new blade",
        ["Noble"] = "something gilded", ["Collector"] = "a curio", ["Royalty"] = "the finest piece",
    };

    /// <summary>The blurb with the want slotted in (the archetype's kind of thing while they're still browsing).</summary>
    public static string Blurb(in Persona p, CustomerDef def, ItemDef want)
    {
        string key = def != null && Browsing.ContainsKey(def.name) ? def.name : def != null && def.customerArcheType == CustomerArcheType.Royalty ? "Royalty" : null;
        return p.Blurb.Replace("{want}", want != null ? want.displayName : key != null ? Browsing[key] : "something");
    }
}
