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
                ParseUnlock(row.Cell(8).GetString(), row.Cell(9).GetString())))
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
                    row.Cell(7).GetValue<bool>()))
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
            "MoneyCost", "BuildCost", "UnlockMoney", "UnlockMaterials");

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
            "Id", "DisplayName", "Kind", "UnlockedByDefault", "UnlockMoney", "UnlockMaterials", "IsStub");
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
        }

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