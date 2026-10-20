using CargoPlan.Core.Missions;
using CargoPlan.Core.Models;
using CargoPlan.Import.ContractTracker;
using Microsoft.Data.Sqlite;

namespace CargoPlan.Import.Tests;

public class ContractTrackerReaderTests
{
    [Fact]
    public void Renamed_column_reads_nothing_and_gives_the_notice()
    {
        using var database = new TempContractDatabase();
        AddStimsSplit(database);
        using (var connection = database.Open())
        using (var rename = connection.CreateCommand())
        {
            rename.CommandText = "ALTER TABLE \"Contracts\" RENAME COLUMN \"RawOcrText\" TO \"OcrText\"";
            rename.ExecuteNonQuery();
        }

        var result = ContractTrackerReader.ReadSince(database.Path, 0);

        result.Candidates.Should().BeEmpty();
        result.Notice.Should().Be(ContractTrackerReader.SchemaNotSupportedNotice);
    }

    [Fact]
    public void Changed_column_type_reads_nothing_and_gives_the_notice()
    {
        using var database = new TempContractDatabase(
            ImportFixtures.Schema.Replace("\"Amount\" INTEGER NOT NULL", "\"Amount\" TEXT NOT NULL", StringComparison.Ordinal));
        AddStimsSplit(database);

        var result = ContractTrackerReader.ReadSince(database.Path, 0);

        result.Candidates.Should().BeEmpty();
        result.Notice.Should().Be(ContractTrackerReader.SchemaNotSupportedNotice);
    }

    [Fact]
    public void More_columns_and_tables_than_the_fingerprint_are_ignored()
    {
        var schema = ImportFixtures.Schema.Replace("\"Name\" TEXT NOT NULL,", "\"Name\" TEXT NOT NULL, \"Reward\" INTEGER NULL,",
                         StringComparison.Ordinal)
                     + "CREATE TABLE \"Sessions\" (\"Id\" INTEGER NOT NULL PRIMARY KEY);";
        using var database = new TempContractDatabase(schema);
        AddStimsSplit(database);

        var result = ContractTrackerReader.ReadSince(database.Path, 0);

        result.Notice.Should().BeNull();
        result.Candidates.Should().ContainSingle();
    }

    [Fact]
    public void Split_capture_is_one_candidate_with_both_ids()
    {
        using var database = new TempContractDatabase();
        AddStimsSplit(database);

        var candidate = ContractTrackerReader.ReadSince(database.Path, 0).Candidates.Should().ContainSingle().Subject;

        candidate.ContractIds.Should().Equal(32, 33);
        candidate.Stops.Should().BeEquivalentTo(
            [
                new MissionStop("Thundering Express Station", 1, [new MissionItem("Stims", 4, CrateSize.Four)]),
                new MissionStop("Faithful Dream Station", 2, [new MissionItem("Stims", 5, CrateSize.Four)]),
                new MissionStop("High Course Station", 3, [new MissionItem("Stims", 3, CrateSize.Two)]),
            ],
            o => o.WithStrictOrdering());
        candidate.CrateSizeAssumed.Should().BeTrue();
        candidate.UsedFallbackRows.Should().BeFalse();
        candidate.ListedDestinationCount.Should().Be(3);
        candidate.DestinationsIncomplete.Should().BeFalse();
        candidate.RawText.Should().Be(ImportFixtures.RawText(ImportFixtures.StimsSplit));
    }

    [Fact]
    public void Capture_from_the_accepted_tab_is_one_candidate_with_full_names()
    {
        using var database = new TempContractDatabase();
        var raw = ImportFixtures.RawText(ImportFixtures.StimsAcceptedTab);
        database.AddContract(37, "Stims", raw, (4, "High Course Hurston's L5 Lagrange point"));
        database.AddContract(38, "Stims", raw,
            (4, "Thunde oss Station at Hurston's L3 Lagrange point. - Freight elevator at Green Glade Station at Hurston's L1 Lagrange point"));

        var candidate = ContractTrackerReader.ReadSince(database.Path, 0).Candidates.Should().ContainSingle().Subject;

        candidate.ContractIds.Should().Equal(37, 38);
        candidate.Stops.Select(s => s.Destination).Should().Equal("Green Glade Station", "High Course Station", "Thundering Express Station");
        candidate.UsedFallbackRows.Should().BeFalse();
        candidate.DestinationsIncomplete.Should().BeFalse();
    }

    [Fact]
    public void Same_text_with_a_gap_in_the_ids_is_two_candidates()
    {
        using var database = new TempContractDatabase();
        var raw = ImportFixtures.RawText(ImportFixtures.StimsSplit);
        database.AddContract(40, "Stims", raw, (4, "Thundering Express Station"));
        database.AddContract(42, "Stims", raw, (3, "High Course Station"));

        var result = ContractTrackerReader.ReadSince(database.Path, 0);

        result.Candidates.Select(c => c.ContractIds).Should().BeEquivalentTo([new long[] { 40 }, [42]], o => o.WithStrictOrdering());
    }

    [Fact]
    public void Consecutive_ids_with_different_text_are_two_candidates()
    {
        using var database = new TempContractDatabase();
        database.AddContract(30, "Silicon", ImportFixtures.RawText(ImportFixtures.SiliconMaxSize), (10, "Melodic Fields Station"));
        database.AddContract(31, "Waste", ImportFixtures.RawText(ImportFixtures.WasteBiggerThan), (9, "Everus Harbor above Hurston"));

        var result = ContractTrackerReader.ReadSince(database.Path, 0);

        result.Candidates.Select(c => c.ContractIds).Should().BeEquivalentTo([new long[] { 30 }, [31]], o => o.WithStrictOrdering());
    }

    [Fact]
    public void Two_commodities_of_one_capture_are_one_stop_with_two_items()
    {
        using var database = new TempContractDatabase();
        var raw = ImportFixtures.RawText(ImportFixtures.IceAndFood);
        database.AddContract(28, "Pressurized Ice", raw, (7, "Thundering Express A freight elevator at Everus Harbor above Hurston has some"));
        database.AddContract(29, "Processed Food", raw, (9, "Thundering Express Station"));

        var candidate = ContractTrackerReader.ReadSince(database.Path, 0).Candidates.Should().ContainSingle().Subject;

        candidate.ContractIds.Should().Equal(28, 29);
        candidate.Stops.Should().BeEquivalentTo(
        [
            new MissionStop("Thundering Express", 1,
            [
                new MissionItem("Pressurized Ice", 7, CrateSize.Four),
                new MissionItem("Processed Food", 9, CrateSize.Eight),
            ]),
        ]);
    }

    [Fact]
    public void Size_phrase_sets_the_max_crate_size()
    {
        using var database = new TempContractDatabase();
        database.AddContract(30, "Silicon", ImportFixtures.RawText(ImportFixtures.SiliconMaxSize), (10, "Melodic Fields Station"));

        var candidate = ContractTrackerReader.ReadSince(database.Path, 0).Candidates.Should().ContainSingle().Subject;

        candidate.Stops.Should().BeEquivalentTo([new MissionStop("Melodic Fields Station", 1, [new MissionItem("Silicon", 10, CrateSize.One)])]);
        candidate.CrateSizeAssumed.Should().BeFalse();
    }

    [Theory]
    [InlineData("Covalex Independent Contractor\nDETAILS\nPRIMARY OBJECTIVES\nACCEPT OFFER")]
    [InlineData(null)]
    public void No_destination_in_the_text_falls_back_to_the_child_rows(string? raw)
    {
        using var database = new TempContractDatabase();
        database.AddContract(50, "Waste", raw, (9, "Everus Harbor above Hurston"));

        var candidate = ContractTrackerReader.ReadSince(database.Path, 0).Candidates.Should().ContainSingle().Subject;

        candidate.UsedFallbackRows.Should().BeTrue();
        candidate.Stops.Should().BeEquivalentTo([new MissionStop("Everus Harbor", 1, [new MissionItem("Waste", 9, CrateSize.Eight)])]);
        candidate.CrateSizeAssumed.Should().BeTrue();
    }

    [Fact]
    public void Fewer_destinations_than_listed_is_incomplete()
    {
        using var database = new TempContractDatabase();
        const string raw = """
            DETAILS
            Deliver 0/4 SCU of Stims to Thundering Express Station at
            DROP OFF LOCATIONS (ANY ORDER)
            - Freight elevator at Thundering Express Station at Hurston's L3
            - Freight elevator at Faithful Dream Station at Hurston's L2
            ACCEPT OFFER
            """;
        database.AddContract(60, "Stims", raw, (4, "Thundering Express Station"));

        var candidate = ContractTrackerReader.ReadSince(database.Path, 0).Candidates.Should().ContainSingle().Subject;

        candidate.ListedDestinationCount.Should().Be(2);
        candidate.DestinationsIncomplete.Should().BeTrue();
    }

    [Fact]
    public void ReadSince_returns_only_ids_above_the_watermark()
    {
        using var database = new TempContractDatabase();
        database.AddContract(30, "Silicon", ImportFixtures.RawText(ImportFixtures.SiliconMaxSize), (10, "Melodic Fields Station"));
        database.AddContract(31, "Waste", ImportFixtures.RawText(ImportFixtures.WasteBiggerThan), (9, "Everus Harbor above Hurston"));
        AddStimsSplit(database);
        database.AddContract(34, "Waste", ImportFixtures.RawText(ImportFixtures.WasteListFragments), (9, "Everus Harbor above Hurston"));
        database.AddContract(36, "Stims", ImportFixtures.RawText(ImportFixtures.StimsOcrErrors), (4, "Thundering Express Station"));

        var result = ContractTrackerReader.ReadSince(database.Path, 33);

        result.Candidates.SelectMany(c => c.ContractIds).Should().Equal(34, 36);
    }

    [Fact]
    public void Read_names_which_of_the_known_ids_are_still_there()
    {
        // Deleted in ContractTracker: the plan's missions whose ids are gone (D39 addendum).
        using var database = new TempContractDatabase();
        database.AddContract(30, "Silicon", ImportFixtures.RawText(ImportFixtures.SiliconMaxSize), (10, "Melodic Fields Station"));
        database.AddContract(36, "Stims", ImportFixtures.RawText(ImportFixtures.StimsOcrErrors), (4, "Thundering Express Station"));

        var result = ContractTrackerReader.Read(database.Path, 36, [30, 31, 36]);

        result.Present.Should().BeEquivalentTo([30L, 36L]);
        result.Candidates.Should().BeEmpty();
    }

    [Fact]
    public void Read_of_a_missing_file_knows_nothing_about_the_known_ids()
    {
        // Not read is not deleted: a missing file must not take missions out of the plan (T5.4).
        var result = ContractTrackerReader.Read(Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid():N}.db"), 0, [30]);

        result.Present.Should().BeNull();
        result.Notice.Should().BeNull();
    }

    [Fact]
    public void Read_of_another_schema_knows_nothing_about_the_known_ids()
    {
        using var database = new TempContractDatabase(ImportFixtures.Schema.Replace("\"RawOcrText\"", "\"OcrText\"", StringComparison.Ordinal));

        ContractTrackerReader.Read(database.Path, 0, [30]).Present.Should().BeNull();
    }

    [Fact]
    public void Read_of_a_file_that_is_no_database_throws()
    {
        // A path to another file: the caller shows a notice (T5.4c), unlike a missing file, which says nothing.
        var path = Path.Combine(Path.GetTempPath(), $"not-a-db-{Guid.NewGuid():N}.db");
        File.WriteAllText(path, "This is not a SQLite database, just text long enough to have a header.");
        try
        {
            var act = () => ContractTrackerReader.Read(path, 0, []);

            act.Should().Throw<SqliteException>();
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Candidate_becomes_a_capture_with_its_ids_and_hints()
    {
        using var database = new TempContractDatabase();
        AddStimsSplit(database);
        var candidate = ContractTrackerReader.ReadSince(database.Path, 0).Candidates.Single();

        var capture = candidate.ToCapture();

        capture.Stops.Should().BeSameAs(candidate.Stops);
        capture.Source.Should().BeEquivalentTo(new ContractSource([32, 33], CrateSizeAssumed: true, ListedDestinations: 3));
    }

    [Fact]
    public void Default_path_is_in_the_roaming_app_data()
    {
        // D38: %APPDATA%\SCContractTracker\contracts.db, one fixed path.
        ContractTrackerReader.DefaultPath.Should().Be(
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SCContractTracker", "contracts.db"));
    }

    [Fact]
    public void Capture_time_is_read_as_utc()
    {
        using var database = new TempContractDatabase();
        database.AddContract(30, "Silicon", ImportFixtures.RawText(ImportFixtures.SiliconMaxSize), "2026-10-20 08:22:54.1193454",
            (10, "Melodic Fields Station"));

        var candidate = ContractTrackerReader.ReadSince(database.Path, 0).Candidates.Should().ContainSingle().Subject;

        candidate.CapturedAt.Should().Be(new DateTimeOffset(2026, 10, 20, 8, 22, 54, TimeSpan.Zero).AddTicks(1193454));
    }

    [Fact]
    public void Unreadable_capture_time_is_null()
    {
        using var database = new TempContractDatabase();
        database.AddContract(30, "Silicon", ImportFixtures.RawText(ImportFixtures.SiliconMaxSize), "yesterday", (10, "Melodic Fields Station"));

        var candidate = ContractTrackerReader.ReadSince(database.Path, 0).Candidates.Should().ContainSingle().Subject;

        candidate.CapturedAt.Should().BeNull();
    }

    [Fact]
    public void Missing_file_reads_nothing_without_a_notice()
    {
        var result = ContractTrackerReader.ReadSince(Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid():N}.db"), 0);

        result.Candidates.Should().BeEmpty();
        result.Notice.Should().BeNull();
    }

    [Fact]
    public void Locked_database_reads_nothing_without_a_notice()
    {
        using var database = new TempContractDatabase();
        AddStimsSplit(database);
        using var writer = database.Open();
        using var lockCommand = writer.CreateCommand();
        lockCommand.CommandText = "BEGIN EXCLUSIVE";
        lockCommand.ExecuteNonQuery();

        var result = ContractTrackerReader.ReadSince(database.Path, 0, busyTimeoutSeconds: 1);

        result.Candidates.Should().BeEmpty();
        result.Notice.Should().BeNull();
        result.Present.Should().BeNull();
    }

    private static void AddStimsSplit(TempContractDatabase database)
    {
        // CT's rows for this capture: 2 + 1 destinations, byte-identical raw text, consecutive ids.
        var raw = ImportFixtures.RawText(ImportFixtures.StimsSplit);
        database.AddContract(32, "Stims", raw,
            (4, "Thundering Express Station at Hurston's L3 Lagrange point"),
            (5, "Faithful Dream Station at chance you can knock this one out for me Hursto Doesn't matter what order you drop them off"));
        database.AddContract(33, "Stims", raw, (3, "High Course Station at - Freight elevator at Thundering Express Station at Hurston's L3 Hurston's Lagrange point"));
    }
}
