using CargoPlan.Core.Routing;
using CargoPlan.Import.Contracts;

namespace CargoPlan.Import.Tests;

/// <summary>
/// The contract title (T13.1): contractor, rank, size, direct, a template's identity with the reward. Covalex from real
/// captures; Red Wind as the card shows it (issue #22) and as the game data name it (synthetic, no capture has it).
/// </summary>
public class ContractTitleTests
{
    private static readonly ContractTitle ExperiencedMedium = new(HaulContractor.Covalex, HaulRank.Experienced, HaulSize.Medium, Direct: false);
    private static readonly ContractTitle MemberSmall = new(HaulContractor.Covalex, HaulRank.Member, HaulSize.Small, Direct: false);
    private static readonly ContractTitle RookieDirectMedium = new(HaulContractor.Covalex, HaulRank.Rookie, HaulSize.Medium, Direct: true);
    private static readonly ContractTitle RookieDirectXs = new(HaulContractor.Covalex, HaulRank.Rookie, HaulSize.Xs, Direct: true);
    private static readonly ContractTitle RookieXs = new(HaulContractor.Covalex, HaulRank.Rookie, HaulSize.Xs, Direct: false);
    private static readonly ContractTitle JuniorSmall = new(HaulContractor.Covalex, HaulRank.Junior, HaulSize.Small, Direct: false);

    // Issue #22, Pyro: "Member | Stellar Medium Haul | to Ruin Station". Direct isn't in that form, so it isn't known.
    private static readonly ContractTitle RedWindMemberStellarMedium =
        new(HaulContractor.RedWind, HaulRank.Member, HaulSize.Medium, Direct: null, AreaLevel.Stellar);

    /// <summary>Every raw text fixture with the title it shows; null where the capture has none.</summary>
    private static readonly Dictionary<string, ContractTitle?> Expected = new(StringComparer.Ordinal)
    {
        [ImportFixtures.CarbonOrCut] = RedWindMemberStellarMedium,
        [ImportFixtures.FuelAccepted] = ExperiencedMedium,
        [ImportFixtures.FuelOffer] = ExperiencedMedium,
        [ImportFixtures.HurstonAnyOrder] = MemberSmall,
        [ImportFixtures.IceAndFood] = null,
        [ImportFixtures.IronCouldntHandle] = RookieDirectMedium,
        [ImportFixtures.PotassiumOrCut] = RedWindMemberStellarMedium,
        [ImportFixtures.SiliconAtMost] = RookieDirectMedium,
        [ImportFixtures.SiliconMaxSize] = RookieDirectXs,
        [ImportFixtures.SiliconOverlayText] = RookieDirectXs,
        [ImportFixtures.SiliconTypingIn] = RookieDirectXs,
        [ImportFixtures.StimsAcceptedTab] = JuniorSmall,
        [ImportFixtures.StimsOcrErrors] = JuniorSmall,
        [ImportFixtures.StimsSplit] = JuniorSmall,
        [ImportFixtures.WasteBiggerThan] = JuniorSmall,
        [ImportFixtures.WasteCollectWrapped] = JuniorSmall,
        [ImportFixtures.WasteListFragments] = JuniorSmall,
        [ImportFixtures.WasteLs] = JuniorSmall,
        [ImportFixtures.WasteOrSmaller] = RedWindMemberStellarMedium,
        [ImportFixtures.WasteTornAbov] = JuniorSmall,
        [ImportFixtures.WasteTornAboy] = RookieXs,
    };

    public static TheoryData<string> AllRawTextFixtures() => ContractTextParserTests.AllRawTextFixtures();

    [Theory]
    [MemberData(nameof(AllRawTextFixtures))]
    public void Every_fixture_reads_the_title_it_shows(string fixture)
    {
        Expected.Should().ContainKey(fixture, "a new fixture gets its title here");

        ScannedContract.Read(ImportFixtures.RawText(fixture)).Title.Should().Be(Expected[fixture]);
    }

    [Theory]
    // Synthetic: ranks, sizes and forms no capture has yet (T13.0a V2/V3, game data of ships-2026.11.2).
    [InlineData("Trainee Rank - Bulk Cargo Haul", HaulRank.Trainee, HaulSize.Bulk, false)]
    [InlineData("Senior Rank - Direct Large Cargo Haul", HaulRank.Senior, HaulSize.Large, true)]
    [InlineData("Master Rank - Extra Small Cargo Haul", HaulRank.Master, HaulSize.Xs, false)]
    [InlineData("Experienced Rank - Direct Medium Cargo Haul", HaulRank.Experienced, HaulSize.Medium, true)]
    // OCR as it reads: another dash, no spaces round it, l and 1 for I and L, capitals.
    [InlineData("Junior Rank – Small Cargo Haul", HaulRank.Junior, HaulSize.Small, false)]
    [InlineData("Junior Rank—Small Cargo Haul", HaulRank.Junior, HaulSize.Small, false)]
    [InlineData("Junior Rank-Small Cargo Haul", HaulRank.Junior, HaulSize.Small, false)]
    [InlineData("Junior Rank Small Cargo Haul", HaulRank.Junior, HaulSize.Small, false)]
    [InlineData("JunIor Rank - SmaIl Cargo Hau1", HaulRank.Junior, HaulSize.Small, false)]
    [InlineData("ROOKIE RANK - DIRECT EXTRA SMALL CARGO HAUL", HaulRank.Rookie, HaulSize.Xs, true)]
    public void Covalex_title_gives_rank_size_and_direct(string title, HaulRank rank, HaulSize size, bool direct)
    {
        ScannedContract.Read(Offer(title)).Title.Should().Be(new ContractTitle(HaulContractor.Covalex, rank, size, direct));
    }

    [Theory]
    // Synthetic: the game data's DisplayTitle (hauling-templates.json), which no capture shows (T13.1 finding, issue #22).
    [InlineData("Senior Hauler Needed for Large Shipment", HaulRank.Senior, HaulSize.Large, false)]
    [InlineData("Experienced Hauler Needed for Direct Medium Shipment", HaulRank.Experienced, HaulSize.Medium, true)]
    [InlineData("Trainee Hauler Needed for Extra Small Shipment", HaulRank.Trainee, HaulSize.Xs, false)]
    public void Red_wind_title_of_the_game_data_gives_rank_size_and_direct(string title, HaulRank rank, HaulSize size, bool direct)
    {
        ScannedContract.Read(Offer(title)).Title.Should().Be(new ContractTitle(HaulContractor.RedWind, rank, size, direct));
    }

    [Theory]
    // Synthetic variants of the card's form (issue #22): Direct only when it stands there, else unknown, never false.
    [InlineData("Senior | Stellar Direct Large Haul | to Everus Harbor", HaulRank.Senior, HaulSize.Large, true, AreaLevel.Stellar)]
    [InlineData("Junior | Planetary Small Haul | from Ruin Station", HaulRank.Junior, HaulSize.Small, null, AreaLevel.Planetary)]
    [InlineData("Member l Stellar Medium Haul l to Ruin", HaulRank.Member, HaulSize.Medium, null, AreaLevel.Stellar)]
    [InlineData("Member Stellar Medium Haul", HaulRank.Member, HaulSize.Medium, null, AreaLevel.Stellar)]
    // An area word the parser doesn't know: the rest of the title still holds.
    [InlineData("Member | Orbital Medium Haul | to Ruin Station", HaulRank.Member, HaulSize.Medium, null, null)]
    public void Red_wind_title_of_the_card_gives_rank_size_and_area(string title, HaulRank rank, HaulSize size, bool? direct, AreaLevel? area)
    {
        ScannedContract.Read(Offer(title)).Title.Should().Be(new ContractTitle(HaulContractor.RedWind, rank, size, direct, area));
    }

    [Theory]
    [InlineData("Chief Rank - Small Cargo Haul")]
    [InlineData("Junior Rank - Huge Cargo Haul")]
    [InlineData("Junior Rank - Small")]
    [InlineData("Junior Rank - Small Cargo Haul for the long run")]
    [InlineData("Captain Hauler Needed for Large Shipment")]
    [InlineData("Member | Stellar Massive Haul | to Ruin Station")]
    [InlineData("")]
    public void A_title_not_read_whole_is_none_and_the_contract_still_reads(string title)
    {
        var scanned = ScannedContract.Read(Offer(title));

        scanned.Title.Should().BeNull();
        scanned.Stops.Should().ContainSingle();
        scanned.Reward.Should().Be(50_250);
    }

    [Fact]
    public void The_first_title_counts()
    {
        // The card repeats a Red Wind title in capitals further down; a second, different one would be noise.
        var text = Offer("Junior Rank - Small Cargo Haul").Replace("DETAILS\n", "DETAILS\nSenior Rank - Direct Large Cargo Haul\n", StringComparison.Ordinal);

        ScannedContract.Read(text).Title.Should().Be(JuniorSmall);
    }

    [Fact]
    public void The_title_leaves_the_content_key_as_it_was()
    {
        var with = ScannedContract.Read(Offer("Junior Rank - Small Cargo Haul"));
        var without = ScannedContract.Read(Offer(""));

        with.Key.Should().Be(without.Key);
    }

    // --- Why no title (E1 of the T13.3a approval) ---

    [Theory]
    [MemberData(nameof(AllRawTextFixtures))]
    public void A_fixture_without_a_title_is_a_title_unreadable_never_another_form(string fixture)
    {
        // Every fixture is Covalex or Red Wind: where the title is missing, OCR lost it.
        var scanned = ScannedContract.Read(ImportFixtures.RawText(fixture));

        scanned.TitleMiss.Should().Be(scanned.Title is null ? TitleMiss.Unreadable : null);
    }

    [Theory]
    // Martin's scans.log (24 Nov 2026): Ling Family Hauling, title read whole, a contract no hauling template has.
    [InlineData("Cargo Hauling Opportunity with Ling Hauling", "Contracted By Ling Family Hauling")]
    // ContractTracker's texts put the contractor on the next line.
    [InlineData("Cargo Hauling Opportunity with Ling Hauling", "Contracted By\nLing Family Hauling")]
    [InlineData("", "Contracted By Ling Family Hauling")]
    public void Another_contractor_without_a_title_is_another_form(string title, string contractor)
    {
        var scanned = ScannedContract.Read(Offer(title, contractor));

        scanned.Title.Should().BeNull();
        scanned.TitleMiss.Should().Be(TitleMiss.OtherForm);
        scanned.NotObservedReason.Should().Be("no hauling template form");
    }

    [Theory]
    // A hauling title OCR broke: a rank before "Rank", "Cargo Haul" at the end, the Red Wind forms; whoever contracted it.
    [InlineData("Junior Rank - Huge Cargo Haul", "Contracted By Ling Family Hauling")]
    [InlineData("Junior Rank - Small Cargo Hal", "Contracted By Ling Family Hauling")]
    [InlineData("Junior Rnk - Small Cargo Haul", "Contracted By Ling Family Hauling")]
    [InlineData("Captain Hauler Needed for Large Shipment", "Contracted By Ling Family Hauling")]
    [InlineData("Chief Hauler Needed for Medium Shipment", "Contracted By Ling Family Hauling")]
    [InlineData("Member | Stellar Massive Haul | to Ruin Station", "Contracted By Ling Family Hauling")]
    // No title line: Covalex or Red Wind lost it; no contractor read, nothing tells.
    [InlineData("", "Contracted By Covalex Independent Contractors")]
    [InlineData("", "Contracted By\nRed Wind Linehaul")]
    [InlineData("Cargo Hauling Opportunity with Ling Hauling", "")]
    [InlineData("", "Contracted By")]
    public void A_broken_title_or_a_hauling_contractor_is_a_title_unreadable(string title, string contractor)
    {
        var scanned = ScannedContract.Read(Offer(title, contractor));

        scanned.Title.Should().BeNull();
        scanned.TitleMiss.Should().Be(TitleMiss.Unreadable);
        scanned.NotObservedReason.Should().Be("title unreadable");
    }

    [Theory]
    [InlineData("Junior Rank - Small Cargo Haul", "Reward 50,250", null)]
    [InlineData("Junior Rank - Small Cargo Haul", "Reward", "reward unreadable")]
    [InlineData("", "Reward 50,250", "title unreadable")]
    [InlineData("", "Reward", "title and reward unreadable")]
    public void The_reason_names_what_wasnt_read(string title, string reward, string? why)
    {
        var text = Offer(title, "Contracted By Covalex Independent Contractors").Replace("Reward 50,250", reward, StringComparison.Ordinal);

        ScannedContract.Read(text).NotObservedReason.Should().Be(why);
    }

    [Fact]
    public void Another_form_needs_no_reward()
    {
        // Not a template's contract: what it pays doesn't matter.
        var text = Offer("Cargo Hauling Opportunity with Ling Hauling", "Contracted By Ling Family Hauling").Replace("Reward 50,250", "Reward", StringComparison.Ordinal);

        ScannedContract.Read(text).NotObservedReason.Should().Be("no hauling template form");
    }

    /// <summary>A whole offer as our OCR reads it (title, reward, details, one delivery, button), with <paramref name="title"/> first.</summary>
    private static string Offer(string title) =>
        $"{title}\nReward 50,250\nContract Availability\nDETAILS\nPRIMARY OBJECTIVES\n" +
        "Deliver 0/9 SCU of Stims to Green Glade Station above Hurston.\nCollect Stims from Everus Harbor.\nACCEPT OFFER\n";

    /// <summary>As <see cref="Offer(string)"/>, with the contractor's line (or two) after the availability, as the card has it.</summary>
    private static string Offer(string title, string contractor) =>
        Offer(title).Replace("Contract Availability\n", $"Contract Availability\n{(contractor.Length > 0 ? contractor + "\n" : "")}", StringComparison.Ordinal);
}
