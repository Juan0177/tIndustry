using System.Text.Json.Nodes;
using Xunit;

namespace TIndustry.AgentPlay.Tests;

/// <summary>The compact answers drop repeats and defaults only: what is left still says everything.</summary>
public sealed class CompactTests
{
    [Fact]
    public void NullsAndEmptiesGoZerosAndCellsStay()
    {
        var answer = JsonNode.Parse("""{"ok":true,"a":null,"b":[],"c":{},"d":{"e":[]},"tick":0,"cella":{"x":0,"y":0}}""")!.AsObject();
        Compact.Apply("trova", answer);
        Assert.Equal("""{"ok":true,"tick":0,"cella":{"x":0,"y":0}}""", answer.ToJsonString());
    }

    [Fact]
    public void ALineBuiltCellByCellIsOneEvent()
    {
        var events = JsonNode.Parse("""
            [{"tick":1,"tipo":"rifiuto","comando":"piazza","codice":"Occupied","cella":{"x":1,"y":1}},
             {"tick":2,"tipo":"rifiuto","comando":"piazza","codice":"Occupied","cella":{"x":2,"y":1}},
             {"tick":46,"giocatore":0,"tipo":"costruito","cantiere":2,"struttura":"miner","cella":{"x":5,"y":5}},
             {"tick":88,"giocatore":0,"tipo":"costruito","cantiere":4,"struttura":"conveyor-basic","cella":{"x":6,"y":1}},
             {"tick":130,"giocatore":0,"tipo":"costruito","cantiere":5,"struttura":"conveyor-basic","cella":{"x":6,"y":2}},
             {"tick":172,"giocatore":0,"tipo":"costruito","cantiere":6,"struttura":"conveyor-basic","cella":{"x":6,"y":3}},
             {"tick":200,"tipo":"inputReale"}]
            """)!.AsArray();
        var compact = Compact.Events(events);
        // Refusals keep their own tick (the journal's); builds of one structure in a row become one entry.
        Assert.Equal(5, compact.Count);
        Assert.Equal("""{"tick":1,"tipo":"rifiuto","comando":"piazza","codice":"Occupied","cella":{"x":1,"y":1}}""", compact[0]!.ToJsonString());
        Assert.Equal("""{"tick":2,"tipo":"rifiuto","comando":"piazza","codice":"Occupied","cella":{"x":2,"y":1}}""", compact[1]!.ToJsonString());
        Assert.Equal("""{"tick":46,"tipo":"costruito","struttura":"miner","cella":{"x":5,"y":5}}""", compact[2]!.ToJsonString());
        Assert.Equal(
            """{"tick":88,"fino":172,"tipo":"costruito","struttura":"conveyor-basic","celle":[{"x":6,"y":1},{"x":6,"y":2},{"x":6,"y":3}]}""",
            compact[3]!.ToJsonString());
        Assert.Equal("""{"tick":200,"tipo":"inputReale"}""", compact[4]!.ToJsonString());
    }

    [Fact]
    public void ALineSaysItsDirectionOnce()
    {
        var same = JsonNode.Parse("""{"tick":4,"celle":[{"x":1,"y":2,"direzione":"sud"},{"x":1,"y":3,"direzione":"sud","incrocio":true}],"rifiuti":[]}""")!.AsObject();
        Compact.Apply("piazza", same);
        Assert.Equal("""{"tick":4,"celle":[{"x":1,"y":2},{"x":1,"y":3,"incrocio":true}],"direzione":"sud"}""", same.ToJsonString());

        var mixed = JsonNode.Parse("""{"celle":[{"x":1,"y":2,"direzione":"sud"},{"x":2,"y":2,"direzione":"est"}]}""")!.AsObject();
        Compact.Apply("piazza", mixed);
        Assert.Equal("""{"celle":[{"x":1,"y":2,"direzione":"sud"},{"x":2,"y":2,"direzione":"est"}]}""", mixed.ToJsonString());
    }

    [Fact]
    public void StatoKeepsOnlyWhatIsThere()
    {
        var stato = JsonNode.Parse("""
            {"edifici":{"minatori":1,"forni":0},"core":{"livello":0,"venditaAutomatica":false},
             "costruzione":{"piani":1,"cantieri":0,"primiPiani":[{"x":1,"y":1,"rimozione":false}],
                            "primiCantieri":[{"x":2,"y":2,"progresso":0.6038649,"smontaggio":true}]},
             "rifiuti":[{"tick":1,"tipo":"rifiuto","codice":"Wall"}],"livelloCampagna":null}
            """)!.AsObject();
        Compact.Apply("stato", stato);
        Assert.Equal(
            """{"edifici":{"minatori":1},"core":{"livello":0},"costruzione":{"piani":1,"cantieri":0,"primiPiani":[{"x":1,"y":1}],"primiCantieri":[{"x":2,"y":2,"progresso":0.6,"smontaggio":true}]},"rifiuti":[{"tick":1,"codice":"Wall"}]}""",
            stato.ToJsonString());
    }

    [Fact]
    public void UiNamesAreTheShortestEndClicStillFinds()
    {
        var all = new List<(string Name, string Text, string Tooltip)>
        {
            ("Hud/Menu/Ricerca", "Ricerca", "Ricerca (T)"),
            ("Hud/Core/Box0/Label1", "15", ""),
            ("Hud/Core/Box1/Label1", "10", ""),
            ("Hud/Panel/Label1", "Ricerca", "")
        };
        // "Ricerca" is also another control's text: one more segment is needed.
        Assert.Equal("Menu/Ricerca", Compact.ShortName("Hud/Menu/Ricerca", all));
        Assert.Equal("Box0/Label1", Compact.ShortName("Hud/Core/Box0/Label1", all));
        Assert.Equal("Panel/Label1", Compact.ShortName("Hud/Panel/Label1", all));

        var ui = JsonNode.Parse("""{"controlli":[{"nome":"Hud/Menu/Ricerca","tipo":"Button","testo":"","rett":{"x":1,"y":2,"larghezza":3,"altezza":4}}]}""")!.AsObject();
        Compact.Apply("ui", ui);
        Assert.Equal("""{"controlli":[{"nome":"Ricerca","tipo":"Button","rett":[1,2,3,4]}]}""", ui.ToJsonString());
    }

    [Fact]
    public void ASavedJournalGivesItsCount()
    {
        var saved = JsonNode.Parse("""{"voci":[{"tick":0},{"tick":1}],"fine":5,"rigiocabile":true,"percorso":"x.json"}""")!.AsObject();
        Compact.Apply("diario", saved);
        Assert.Equal("""{"fine":5,"rigiocabile":true,"percorso":"x.json","numeroVoci":2}""", saved.ToJsonString());
    }
}
