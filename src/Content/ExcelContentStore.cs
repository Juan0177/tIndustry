using System.Globalization;
using ClosedXML.Excel;

namespace TIndustry.Logistics;

public static class ExcelContentStore
{
    public static GameContent Load(string path)
    {
        using var workbook = new XLWorkbook(path);
        var conveyorsSheet = workbook.Worksheet("Conveyors");
        var recipesSheet = workbook.Worksheet("Recipes");

        var conveyors = conveyorsSheet.RowsUsed()
            .Skip(1)
            .Where(row => !row.Cell(1).IsEmpty())
            .Select(row => new ConveyorDefinition(
                row.Cell(1).GetString(),
                row.Cell(2).GetValue<int>(),
                row.Cell(3).GetValue<float>(),
                row.Cell(4).GetValue<int>(),
                row.Cell(5).GetValue<float>(),
                row.Cell(6).GetValue<int>(),
                ParseAmounts(row.Cell(7).GetString()),
                ParseUnlock(row.Cell(8).GetString(), row.Cell(9).GetString()),
                ParseLogisticsKind(row.Cell(10).IsEmpty() ? "belt" : row.Cell(10).GetString())))
            .ToArray();

        var recipes = recipesSheet.RowsUsed()
            .Skip(1)
            .Where(row => !row.Cell(1).IsEmpty())
            .Select(row => new RecipeDefinition(
                row.Cell(1).GetString(),
                row.Cell(2).GetValue<float>(),
                ParseAmounts(row.Cell(3).GetString()),
                ParseAmounts(row.Cell(4).GetString())))
            .ToArray();

        IReadOnlyList<StructureDefinition> structures = [];
        if (workbook.Worksheets.Any(sheet => sheet.Name == "Structures"))
        {
            var structuresSheet = workbook.Worksheet("Structures");
            structures = structuresSheet.RowsUsed()
                .Skip(1)
                .Where(row => !row.Cell(1).IsEmpty())
                .Select(row => new StructureDefinition(
                    row.Cell(1).GetString(),
                    row.Cell(2).GetString(),
                    Enum.Parse<StructureKind>(row.Cell(3).GetString(), ignoreCase: true),
                    row.Cell(4).GetValue<bool>(),
                    ParseUnlock(row.Cell(5).GetString(), row.Cell(6).GetString()),
                    row.Cell(7).GetValue<bool>(),
                    ParseIdList(row.Cell(8).IsEmpty() ? "" : row.Cell(8).GetString())))
                .ToArray();
        }

        var content = new GameContent
        {
            Conveyors = conveyors,
            Recipes = recipes,
            Structures = structures
        };
        if (content.Structures.Count == 0)
        {
            content.Structures = GameContent.BuildLegacyStructures(content);
        }

        if (workbook.Worksheets.Any(sheet => sheet.Name == "Market"))
        {
            var marketSheet = workbook.Worksheet("Market");
            content.Market = marketSheet.RowsUsed()
                .Skip(1)
                .Where(row => !row.Cell(1).IsEmpty())
                .Select(row => new MarketItemDefinition(
                    row.Cell(1).GetString(),
                    row.Cell(2).GetString(),
                    row.Cell(3).GetValue<int>()))
                .ToArray();
        }

        if (workbook.Worksheets.Any(sheet => sheet.Name == "Buildings"))
        {
            var buildingsSheet = workbook.Worksheet("Buildings");
            content.Buildings = buildingsSheet.RowsUsed()
                .Skip(1)
                .Where(row => !row.Cell(1).IsEmpty())
                .Select(row => new BuildingDefinition(
                    row.Cell(1).GetString(),
                    row.Cell(2).GetValue<int>(),
                    ParseAmounts(row.Cell(3).GetString()),
                    row.Cell(4).IsEmpty() ? 100 : row.Cell(4).GetValue<int>()))
                .ToArray();
        }

        if (workbook.Worksheets.Any(sheet => sheet.Name == "Economy"))
        {
            var economySheet = workbook.Worksheet("Economy");
            var row = economySheet.Row(2);
            content.Economy = new EconomyConfig(
                new CoreUpgradeDefinition(
                    row.Cell(1).GetValue<int>(),
                    ParseAmounts(row.Cell(2).GetString()),
                    row.Cell(3).GetValue<int>()),
                row.Cell(4).GetString());
        }

        if (content.Market.Count == 0)
        {
            content.Market = MarketCatalog.CreateDefault().Items.ToList();
        }

        if (content.Buildings.Count == 0)
        {
            content.Buildings =
            [
                new BuildingDefinition("miner", 25, [new ResourceAmount("iron-plate", 4)], 100),
                new BuildingDefinition("smelter", 40, [new ResourceAmount("iron-plate", 6)], 100)
            ];
        }

        content.Economy ??= new EconomyConfig(
            new CoreUpgradeDefinition(150, [new ResourceAmount("iron-plate", 20)], 25),
            "Rimozione edifici/nastri: rimborso completo (100%).");

        return content;
    }

    public static void Save(string path, GameContent content)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        using var workbook = new XLWorkbook();
        var conveyorsSheet = workbook.AddWorksheet("Conveyors");
        WriteHeaders(conveyorsSheet,
            "Id", "Tier", "RateItemsPerSecond", "Capacity", "ItemSpacing",
            "MoneyCost", "BuildCost", "UnlockMoney", "UnlockMaterials", "Kind");

        for (var index = 0; index < content.Conveyors.Count; index++)
        {
            var definition = content.Conveyors[index];
            var row = index + 2;
            conveyorsSheet.Cell(row, 1).Value = definition.Id;
            conveyorsSheet.Cell(row, 2).Value = definition.Tier;
            conveyorsSheet.Cell(row, 3).Value = definition.RateItemsPerSecond;
            conveyorsSheet.Cell(row, 4).Value = definition.Capacity;
            conveyorsSheet.Cell(row, 5).Value = definition.ItemSpacing;
            conveyorsSheet.Cell(row, 6).Value = definition.MoneyCost;
            conveyorsSheet.Cell(row, 7).Value = FormatAmounts(definition.BuildCost);
            conveyorsSheet.Cell(row, 8).Value = definition.Unlock?.Money ?? 0;
            conveyorsSheet.Cell(row, 9).Value = FormatAmounts(definition.Unlock?.Materials ?? []);
            conveyorsSheet.Cell(row, 10).Value = definition.Kind.ToString().ToLowerInvariant();
        }

        var recipesSheet = workbook.AddWorksheet("Recipes");
        WriteHeaders(recipesSheet, "Id", "DurationSeconds", "Inputs", "Outputs");
        for (var index = 0; index < content.Recipes.Count; index++)
        {
            var recipe = content.Recipes[index];
            var row = index + 2;
            recipesSheet.Cell(row, 1).Value = recipe.Id;
            recipesSheet.Cell(row, 2).Value = recipe.DurationSeconds;
            recipesSheet.Cell(row, 3).Value = FormatAmounts(recipe.Inputs);
            recipesSheet.Cell(row, 4).Value = FormatAmounts(recipe.Outputs);
        }

        var structuresSheet = workbook.AddWorksheet("Structures");
        WriteHeaders(structuresSheet,
            "Id", "DisplayName", "Kind", "UnlockedByDefault", "UnlockMoney", "UnlockMaterials", "IsStub",
            "Prerequisites");
        for (var index = 0; index < content.Structures.Count; index++)
        {
            var structure = content.Structures[index];
            var row = index + 2;
            structuresSheet.Cell(row, 1).Value = structure.Id;
            structuresSheet.Cell(row, 2).Value = structure.DisplayName;
            structuresSheet.Cell(row, 3).Value = structure.Kind.ToString();
            structuresSheet.Cell(row, 4).Value = structure.UnlockedByDefault;
            structuresSheet.Cell(row, 5).Value = structure.Unlock?.Money ?? 0;
            structuresSheet.Cell(row, 6).Value = FormatAmounts(structure.Unlock?.Materials ?? []);
            structuresSheet.Cell(row, 7).Value = structure.IsStub;
            structuresSheet.Cell(row, 8).Value = string.Join(';', structure.Requires);
        }

        var marketSheet = workbook.AddWorksheet("Market");
        WriteHeaders(marketSheet, "ItemId", "DisplayName", "SellPrice");
        var market = content.Market.Count > 0 ? content.Market : MarketCatalog.CreateDefault().Items;
        for (var index = 0; index < market.Count; index++)
        {
            var item = market[index];
            var row = index + 2;
            marketSheet.Cell(row, 1).Value = item.ItemId;
            marketSheet.Cell(row, 2).Value = item.DisplayName;
            marketSheet.Cell(row, 3).Value = item.SellPrice;
        }

        var buildingsSheet = workbook.AddWorksheet("Buildings");
        WriteHeaders(buildingsSheet, "Id", "MoneyCost", "BuildCost", "RefundPercent");
        for (var index = 0; index < content.Buildings.Count; index++)
        {
            var building = content.Buildings[index];
            var row = index + 2;
            buildingsSheet.Cell(row, 1).Value = building.Id;
            buildingsSheet.Cell(row, 2).Value = building.MoneyCost;
            buildingsSheet.Cell(row, 3).Value = FormatAmounts(building.BuildCost);
            buildingsSheet.Cell(row, 4).Value = building.RefundPercent;
        }

        var economy = content.GetEconomy();
        var economySheet = workbook.AddWorksheet("Economy");
        WriteHeaders(economySheet, "CoreUpgradeMoney", "CoreUpgradeMaterials", "SaleBonusPercent", "RefundPolicyNote");
        economySheet.Cell(2, 1).Value = economy.CoreUpgrade.MoneyCost;
        economySheet.Cell(2, 2).Value = FormatAmounts(economy.CoreUpgrade.BuildCost);
        economySheet.Cell(2, 3).Value = economy.CoreUpgrade.SaleBonusPercent;
        economySheet.Cell(2, 4).Value = economy.RefundPolicyNote;

        foreach (var sheet in workbook.Worksheets)
        {
            sheet.SheetView.FreezeRows(1);
            sheet.Row(1).Style.Font.Bold = true;
            sheet.Row(1).Style.Fill.BackgroundColor = XLColor.FromHtml("#D5A44C");
            sheet.ColumnsUsed().AdjustToContents();
            sheet.RangeUsed()?.SetAutoFilter();
        }

        workbook.SaveAs(path);
    }

    private static LogisticsKind ParseLogisticsKind(string value) =>
        Enum.TryParse<LogisticsKind>(value, ignoreCase: true, out var kind)
            ? kind
            : LogisticsKind.Belt;

    private static UnlockRequirement? ParseUnlock(string moneyValue, string materialsValue)
    {
        var money = int.TryParse(moneyValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : 0;
        var materials = ParseAmounts(materialsValue);
        return money == 0 && materials.Count == 0
            ? null
            : new UnlockRequirement(money, materials);
    }

    private static IReadOnlyList<string> ParseIdList(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        return value.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private static IReadOnlyList<ResourceAmount> ParseAmounts(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        return value.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(entry => entry.Split(':', 2, StringSplitOptions.TrimEntries))
            .Select(parts => parts.Length == 2 && int.TryParse(parts[1], out var amount)
                ? new ResourceAmount(parts[0], amount)
                : throw new InvalidDataException($"Costo Excel non valido: '{string.Join(':', parts)}'"))
            .ToArray();
    }

    private static string FormatAmounts(IReadOnlyList<ResourceAmount> amounts) =>
        string.Join(';', amounts.Select(entry => $"{entry.ItemId}:{entry.Amount}"));

    private static void WriteHeaders(IXLWorksheet sheet, params string[] headers)
    {
        for (var index = 0; index < headers.Length; index++)
        {
            sheet.Cell(1, index + 1).Value = headers[index];
        }
    }
}