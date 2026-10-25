using CargoPlan.Core.Missions;
using CargoPlan.Core.Models;
using CargoPlan.Core.Planning;
using CargoPlan.Core.Plans;
using CargoPlan.Core.Ships;
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
    public void Capture_with_misread_names_gives_one_stop_per_place()
    {
        // B1: ids 9–12 gave five stops in the game, "Melodic Fieids" and "Thundering Express" among them.
        using var database = new TempContractDatabase();
        var raw = ImportFixtures.RawText(ImportFixtures.HurstonAnyOrder);
        database.AddContract(9, "Pressurized Ice", raw, (6, "Melodic Fieids Hey"));
        database.AddContract(10, "Processed Food", raw, (5, "Faithful Dream Station at Hurston's L2 Lagrange point"));
        database.AddContract(11, "Pressurized Ice", raw, (3, "Thundering Express Lagrange point - Freight elevator at Faithful Dream Station"));
        database.AddContract(12, "Processed Food", raw, (5, "Melodic Fields Station at Hurston's L4 Lagrange point"));

        var candidate = ContractTrackerReader.ReadSince(database.Path, 0).Candidates.Should().ContainSingle().Subject;

        candidate.Stops.Should().BeEquivalentTo(
            [
                new MissionStop("Melodic Fields Station", 1,
                [
                    new MissionItem("Pressurized Ice", 6, CrateSize.Four),
                    new MissionItem("Processed Food", 5, CrateSize.Four),
                ]),
                new MissionStop("Faithful Dream Station", 2, [new MissionItem("Processed Food", 5, CrateSize.Four)]),
                new MissionStop("Thundering Express Station", 3, [new MissionItem("Pressurized Ice", 3, CrateSize.Two)]),
            ],
            o => o.WithStrictOrdering());
        candidate.DestinationsIncomplete.Should().BeFalse();
    }

    [Fact]
    public void Spellings_that_differ_only_by_ocr_letters_are_one_stop()
    {
        using var database = new TempContractDatabase();
        database.AddContract(50, "Stims", """
            DETAILS
            Deliver 0/4 SCU of Stims to Thundering Express Statlon at Hurston's L3.
            Deliver 0/2 SCU of Waste to Thundering Express Station at Hurston's L3.
            ACCEPT OFFER
            """, (4, "Thundering Express Station"));

        var candidate = ContractTrackerReader.ReadSince(database.Path, 0).Candidates.Should().ContainSingle().Subject;

        candidate.Stops.Should().ContainSingle().Which.Items.Should().HaveCount(2);
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

    // --- New database (D44): ids start again at 1, the plan's watermark would hide every capture ---

    [Fact]
    public void A_new_database_below_the_watermark_is_read_from_the_start()
    {
        using var database = AddThreeCaptures(new TempContractDatabase());

        var result = ContractTrackerReader.Read(database.Path, 57, [50, 57]);

        result.DatabaseLooksNew.Should().BeTrue();
        result.Candidates.SelectMany(c => c.ContractIds).Should().Equal(1, 2, 3);
        result.Present.Should().BeEmpty("the plan's ids belong to the old file");
    }

    [Fact]
    public void An_empty_new_database_looks_new_too()
    {
        // Reinstalled, nothing captured yet: the watermark must still go back, or capture 1 never comes in.
        using var database = new TempContractDatabase();

        var result = ContractTrackerReader.Read(database.Path, 57, [57]);

        result.DatabaseLooksNew.Should().BeTrue();
        result.Candidates.Should().BeEmpty();
    }

    [Fact]
    public void Deleting_the_newest_contract_is_no_new_database()
    {
        // MAX(Id) drops to 50 here; the highest id ever given stays 57, so nothing is read twice.
        using var database = new TempContractDatabase();
        database.AddContract(50, "Silicon", ImportFixtures.RawText(ImportFixtures.SiliconMaxSize), (10, "Melodic Fields Station"));
        database.AddContract(57, "Waste", ImportFixtures.RawText(ImportFixtures.WasteBiggerThan), (9, "Everus Harbor above Hurston"));
        using (var connection = database.Open())
        using (var delete = connection.CreateCommand())
        {
            delete.CommandText = "DELETE FROM \"Contracts\" WHERE \"Id\" = 57";
            delete.ExecuteNonQuery();
        }

        var result = ContractTrackerReader.Read(database.Path, 57, [50, 57]);

        result.DatabaseLooksNew.Should().BeFalse();
        result.Candidates.Should().BeEmpty();
        result.Present.Should().BeEquivalentTo([50L]);
    }

    [Fact]
    public void The_same_database_at_the_watermark_is_not_new()
    {
        using var database = AddThreeCaptures(new TempContractDatabase());

        var result = ContractTrackerReader.Read(database.Path, 3, [1, 2, 3]);

        result.DatabaseLooksNew.Should().BeFalse();
        result.Candidates.Should().BeEmpty();
    }

    [Fact]
    public void Without_autoincrement_a_new_database_is_not_detected()
    {
        // No sqlite_sequence, no highest id ever given: read as before, nothing guessed.
        using var database = AddThreeCaptures(new TempContractDatabase(
            ImportFixtures.Schema.Replace("AUTOINCREMENT", "", StringComparison.Ordinal)));

        var result = ContractTrackerReader.Read(database.Path, 57, [57]);

        result.DatabaseLooksNew.Should().BeFalse();
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
    public void Candidate_carries_the_reward_into_the_capture()
    {
        using var database = new TempContractDatabase();
        database.AddContract(47, "Waste", ImportFixtures.RawText(ImportFixtures.WasteTornAboy), (9, "Everus Harbor aboy"));

        var candidate = ContractTrackerReader.ReadSince(database.Path, 0).Candidates.Single();

        candidate.Reward.Should().Be(50_250);
        candidate.ToCapture().Source.Reward.Should().Be(50_250);
    }

    [Fact]
    public void Two_contracts_to_the_same_place_are_no_duplicate_but_a_retry_is()
    {
        // Ids 47/48: Waste 9 to Everus Harbor twice, rewards 50,250 and 72,500. Id 49: a retry of 47, same text.
        using var database = new TempContractDatabase();
        database.AddContract(47, "Waste", ImportFixtures.RawText(ImportFixtures.WasteTornAboy), (9, "Everus Harbor aboy"));
        database.AddContract(48, "Waste", ImportFixtures.RawText(ImportFixtures.WasteTornAbov), (9, "Everus Harbor abov"));
        database.AddContract(49, "Waste", ImportFixtures.RawText(ImportFixtures.WasteTornAboy), (9, "Everus Harbor aboy"));
        var captures = ContractTrackerReader.ReadSince(database.Path, 0).Candidates.Select(c => c.ToCapture()).ToList();
        var ship = ShipLoader.Load("hull_a");

        var saved = ContractSync.Apply(new SavedPlan([], new LoadPlan(ship.Id, [], []), CrateCap.Of(ship)), ship, captures, null);

        var duplicates = ContractSync.Duplicates(saved);
        saved.Missions.Where(m => duplicates.Contains(m.Id)).Select(m => m.Contract!.ContractIds.Single()).Should().Equal(49);
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

    // Three captures with different raw texts, so consecutive ids don't group them into one.
    private static TempContractDatabase AddThreeCaptures(TempContractDatabase database)
    {
        database.AddContract(1, "Silicon", ImportFixtures.RawText(ImportFixtures.SiliconMaxSize), (10, "Melodic Fields Station"));
        database.AddContract(2, "Waste", ImportFixtures.RawText(ImportFixtures.WasteBiggerThan), (9, "Everus Harbor above Hurston"));
        database.AddContract(3, "Stims", ImportFixtures.RawText(ImportFixtures.StimsOcrErrors), (4, "Thundering Express Station"));
        return database;
    }

    // --- Pickup (D68): ContractTracker's pickup text, split at "/", cut to the place's name (B6, D69) ---

    [Fact]
    public void The_pickup_of_a_capture_goes_to_its_items()
    {
        using var database = new TempContractDatabase();
        AddStimsSplit(database);
        database.SetPickup(32, "Everus Harbor");
        database.SetPickup(33, "Everus Harbor");

        var candidate = ContractTrackerReader.ReadSince(database.Path, 0).Candidates.Should().ContainSingle().Subject;

        candidate.Stops.SelectMany(s => s.Items).Should().AllSatisfy(i => i.Pickup.Should().Equal("Everus Harbor"));
    }

    [Theory]
    [InlineData("HUR-L1 Green Glade Station / HUR-L2 Faithful Dream Station", "HUR-L1 Green Glade Station|HUR-L2 Faithful Dream Station")]
    [InlineData("HUR-L4 Melodic Fields / HUR-L1 Green Glad", "HUR-L4 Melodic Fields|HUR-L1 Green Glad")]
    [InlineData("Everus Harbor DROP OFF LOCATIONS (ANY ORDER)", "Everus Harbor")]
    [InlineData("Everus H - Freight elevator at High Course Station", "Everus H")]
    [InlineData("Everus Harbor. - Freight elevator at Green Glade Station at Hurston's L1 Lagrange point", "Everus Harbor")]
    [InlineData("Everus Harbor above Hurston", "Everus Harbor")]
    [InlineData("Everus Harbor.", "Everus Harbor")]
    [InlineData(" Everus Harbor /everus harbor/ ", "Everus Harbor")]
    [InlineData("Everus Harbor / Everus Harbor. - Freight elevator at Green Glade Station", "Everus Harbor")]
    public void The_pickup_text_is_split_at_the_slash_and_cut_to_the_place_name(string pickup, string expected)
    {
        // A name, never the contract's text (B6): ContractTracker's own pickup field holds the next list line (id 38).
        // Spelling noise in the name itself stays as captured (D38); the player corrects it in the editor.
        using var database = new TempContractDatabase();
        database.AddContract(36, "Stims", null, (4, "Thundering Express Station"));
        database.SetPickup(36, pickup);

        var item = ContractTrackerReader.ReadSince(database.Path, 0).Candidates.Single().Stops.Single().Items.Single();

        item.Pickup.Should().Equal(expected.Split('|'));
    }

    [Fact]
    public void An_empty_pickup_text_is_no_pickup()
    {
        using var database = new TempContractDatabase();
        AddStimsSplit(database);

        var candidate = ContractTrackerReader.ReadSince(database.Path, 0).Candidates.Single();

        candidate.Stops.SelectMany(s => s.Items).Should().AllSatisfy(i => i.Pickup.Should().BeNull());
    }

    [Fact]
    public void Each_commodity_of_a_capture_takes_the_pickup_of_its_row()
    {
        using var database = new TempContractDatabase();
        var raw = ImportFixtures.RawText(ImportFixtures.IceAndFood);
        database.AddContract(28, "Pressurized Ice", raw, (7, "Thundering Express Station"));
        database.AddContract(29, "Processed Food", raw, (9, "Thundering Express Station"));
        database.SetPickup(28, "Everus Harbor");
        database.SetPickup(29, "Baijini Point");

        var items = ContractTrackerReader.ReadSince(database.Path, 0).Candidates.Single().Stops.Single().Items;

        items.Select(i => (i.Commodity, i.Pickup![0])).Should().Equal(("Pressurized Ice", "Everus Harbor"), ("Processed Food", "Baijini Point"));
    }

    [Fact]
    public void Without_the_pickup_column_nothing_is_read()
    {
        using var database = new TempContractDatabase();
        AddStimsSplit(database);
        using (var connection = database.Open())
        using (var rename = connection.CreateCommand())
        {
            rename.CommandText = "ALTER TABLE \"Contracts\" RENAME COLUMN \"PickupLocation\" TO \"Pickup\"";
            rename.ExecuteNonQuery();
        }

        var result = ContractTrackerReader.ReadSince(database.Path, 0);

        result.Candidates.Should().BeEmpty();
        result.Notice.Should().Be(ContractTrackerReader.SchemaNotSupportedNotice);
    }

    [Fact]
    public void A_retry_with_another_pickup_text_is_still_a_retry()
    {
        // D38: the same contract gave three pickup texts; the pickup is no part of the duplicate rule.
        using var database = new TempContractDatabase();
        database.AddContract(47, "Waste", ImportFixtures.RawText(ImportFixtures.WasteTornAboy), (9, "Everus Harbor aboy"));
        database.AddContract(49, "Waste", ImportFixtures.RawText(ImportFixtures.WasteTornAboy), (9, "Everus Harbor aboy"));
        database.SetPickup(47, "HUR-L4 Melodic Fields / HUR-L1 Green Glad");
        database.SetPickup(49, "HUR-L4 Melodic Fiel");
        var captures = ContractTrackerReader.ReadSince(database.Path, 0).Candidates.Select(c => c.ToCapture()).ToList();
        var ship = ShipLoader.Load("hull_a");

        var saved = ContractSync.Apply(new SavedPlan([], new LoadPlan(ship.Id, [], []), CrateCap.Of(ship)), ship, captures, null);

        var duplicates = ContractSync.Duplicates(saved);
        saved.Missions.Where(m => duplicates.Contains(m.Id)).Select(m => m.Contract!.ContractIds.Single()).Should().Equal(49);
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
