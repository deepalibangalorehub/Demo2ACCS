using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UniversalTennis.Algorithm.Models;

namespace UniversalTennis.Algorithm
{
    public class SanityCheck
    {
        public static IEnumerable<Graph<int>> FindGroups(List<Result> results, int threshold = 3)
        {
            var g = new Graph<int>();
            foreach (var r in results)
            {
                if (!g.Nodes.Contains(r.Winner1Id))
                    g.AddNode(r.Winner1Id);
                if (!g.Nodes.Contains(r.Loser1Id))
                    g.AddNode(r.Loser1Id);
                g.AddArc(r.Winner1Id, r.Loser1Id);
            }
            var subGraphs = g.GetConnectedComponents();
            return subGraphs.Where(graph => graph.Nodes.Count() <= threshold).ToList();
        }

        public static IEnumerable<Graph<int>> FindGroups(IDictionary<int, List<Result>> resultsDict, int threshold = 3)
        {
            var g = new Graph<int>();
            // flatten dict back to a list of unique results
            var results = resultsDict.ToList().SelectMany(x => x.Value).Distinct();
            foreach (var r in results)
            {
                if (!g.Nodes.Contains(r.Winner1Id))
                    g.AddNode(r.Winner1Id);
                if (!g.Nodes.Contains(r.Loser1Id))
                    g.AddNode(r.Loser1Id);
                g.AddArc(r.Winner1Id, r.Loser1Id);
            }
            var subGraphs = g.GetConnectedComponents();
            return subGraphs.Where(graph => graph.Nodes.Count() <= threshold).ToList();
        }

        public static IEnumerable<Graph<int>> FindDoublesGroups(List<Result> results, int threshold = 3)
        {
            var g = new Graph<int>();
            foreach (var r in results)
            {
                if (!g.Nodes.Contains(r.Winner2Id ?? 0))
                    g.AddNode(r.Winner2Id ?? 0);
                if (!g.Nodes.Contains(r.Loser2Id ?? 0))
                    g.AddNode(r.Loser2Id ?? 0);
                if (!g.Nodes.Contains(r.Winner1Id))
                    g.AddNode(r.Winner1Id);
                if (!g.Nodes.Contains(r.Loser1Id))
                    g.AddNode(r.Loser1Id);
                g.AddArc(r.Winner1Id, r.Loser1Id);
                g.AddArc(r.Winner1Id, r.Loser2Id ?? 0);
                g.AddArc(r.Winner2Id ?? 0, r.Loser1Id);
                g.AddArc(r.Winner2Id ?? 0, r.Loser2Id ?? 0);
            }
            var subGraphs = g.GetConnectedComponents();
            return subGraphs.OrderByDescending(graph => graph.Nodes.Count()).Where(graph => graph.Nodes.Count() <= threshold).ToList();
        }

        public static IEnumerable<Graph<int>> FindDoublesGroups(IDictionary<int, List<Result>> resultsDict, int threshold = 3)
        {
            var g = new Graph<int>();
            var results = resultsDict.ToList().SelectMany(x => x.Value).Distinct();
            foreach (var r in results)
            {
                if (!g.Nodes.Contains(r.Winner2Id ?? 0))
                    g.AddNode(r.Winner2Id ?? 0);
                if (!g.Nodes.Contains(r.Loser2Id ?? 0))
                    g.AddNode(r.Loser2Id ?? 0);
                if (!g.Nodes.Contains(r.Winner1Id))
                    g.AddNode(r.Winner1Id);
                if (!g.Nodes.Contains(r.Loser1Id))
                    g.AddNode(r.Loser1Id);
                g.AddArc(r.Winner1Id, r.Loser1Id);
                g.AddArc(r.Winner1Id, r.Loser2Id ?? 0);
                g.AddArc(r.Winner2Id ?? 0, r.Loser1Id);
            }
            var subGraphs = g.GetConnectedComponents();
            return subGraphs.OrderByDescending(graph => graph.Nodes.Count()).Where(graph => graph.Nodes.Count() <= threshold).ToList();
        }

        public class Connection
        {
            public int X { get; set; }
            public int Y { get; set; }
        }

        public class ConnectionDoubles
        {
            public int A { get; set; }
            public int B { get; set; }
            public int X { get; set; }
            public int Y { get; set; }
        }
    }
}
