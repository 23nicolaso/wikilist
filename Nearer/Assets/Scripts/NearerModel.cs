using System;
using UnityEngine;

namespace Nearer
{
    [Serializable] public class PuzzleBank
    {
        public int schema;
        public string epoch;
        public string extraction;
        public string license;
        public string licenseUrl;
        public Puzzle[] puzzles;
    }

    [Serializable] public class Puzzle
    {
        public string id;
        public int day;
        public string category;
        public string anchor;
        public string anchorTitle;
        public long anchorRevision;
        public string anchorUrl;
        public int anchorLinkCount;
        public float tieTolerance;
        public int informativePairs;
        public Topic[] topics;
    }

    [Serializable] public class Topic
    {
        public string title;
        public string label;
        public long revision;
        public string url;
        public int linkCount;
        public int sharedCount;
        public int unionCount;
        public float similarity;
        public string[] evidence;
    }

    [Serializable] public class RoundState
    {
        public int version = 1;
        public string puzzleId;
        public Vector2[] positions;
        public bool[] placed;
        public bool revealed;
    }

    public struct ScoreResult
    {
        public int correct;
        public int total;
        public int percent;
    }

    public static class Rules
    {
        public const float MinimumRadius = 0.18f;
        public const float MaximumRadius = 0.97f;

        public static int DayIndex(DateTime utc, string epoch, int count)
        {
            if (count < 1) throw new ArgumentOutOfRangeException(nameof(count));
            var origin = DateTime.ParseExact(epoch, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
            int days = (int)(utc.Date - origin.Date).TotalDays;
            return ((days % count) + count) % count;
        }

        public static bool Valid(RoundState state, Puzzle puzzle)
        {
            if (state == null || state.version != 1 || state.puzzleId != puzzle.id ||
                state.positions == null || state.placed == null ||
                state.positions.Length != puzzle.topics.Length || state.placed.Length != puzzle.topics.Length) return false;
            for (int i = 0; i < state.positions.Length; i++)
            {
                var p = state.positions[i];
                if (float.IsNaN(p.x) || float.IsNaN(p.y) || float.IsInfinity(p.x) || float.IsInfinity(p.y) || p.magnitude > 1.3f) return false;
                if (state.placed[i] && (p.magnitude < MinimumRadius - 0.001f || p.magnitude > MaximumRadius + 0.001f)) return false;
            }
            return true;
        }

        public static RoundState NewRound(Puzzle puzzle)
        {
            var state = new RoundState {puzzleId = puzzle.id, positions = new Vector2[puzzle.topics.Length], placed = new bool[puzzle.topics.Length]};
            int[] order = {0, 1, 2, 3, 4};
            uint seed = 2166136261;
            foreach (char c in puzzle.id) seed = unchecked((seed ^ c) * 16777619);
            for (int i = order.Length - 1; i > 0; i--)
            {
                seed = unchecked(seed * 1664525 + 1013904223);
                int j = (int)(seed % (uint)(i + 1));
                int old = order[i]; order[i] = order[j]; order[j] = old;
            }
            for (int i = 0; i < state.positions.Length; i++)
            {
                float angle = (-90 + order[i % order.Length] * 72) * Mathf.Deg2Rad;
                state.positions[i] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 0.93f;
            }
            return state;
        }

        public static Vector2 ClampPosition(Vector2 p)
        {
            if (p.sqrMagnitude < 0.0001f) p = Vector2.up;
            return p.normalized * Mathf.Clamp(p.magnitude, MinimumRadius, MaximumRadius);
        }

        public static bool Complete(RoundState state)
        {
            foreach (bool placed in state.placed) if (!placed) return false;
            return true;
        }

        public static ScoreResult Score(Puzzle puzzle, RoundState state)
        {
            var score = new ScoreResult();
            for (int i = 0; i < puzzle.topics.Length; i++)
            for (int j = i + 1; j < puzzle.topics.Length; j++)
            {
                float truth = puzzle.topics[i].similarity - puzzle.topics[j].similarity;
                if (Mathf.Abs(truth) <= puzzle.tieTolerance) continue;
                score.total++;
                float prediction = state.positions[j].magnitude - state.positions[i].magnitude;
                if (Mathf.Abs(prediction) > 0.012f && Mathf.Sign(prediction) == Mathf.Sign(truth)) score.correct++;
            }
            score.percent = score.total == 0 ? 100 : Mathf.RoundToInt(100f * score.correct / score.total);
            return score;
        }

        // Each correct comparison belongs equally to its two topics. Round cumulative
        // shares so the visible whole-number awards sum exactly to the existing score.
        public static int[] TopicPoints(Puzzle puzzle, RoundState state)
        {
            var result = new int[puzzle.topics.Length];
            var shares = new int[result.Length];
            var score = Score(puzzle, state);
            for (int i = 0; i < result.Length; i++)
            for (int j = i + 1; j < result.Length; j++)
            {
                float truth = puzzle.topics[i].similarity - puzzle.topics[j].similarity;
                float prediction = state.positions[j].magnitude - state.positions[i].magnitude;
                if (Mathf.Abs(truth) <= puzzle.tieTolerance || Mathf.Abs(prediction) <= 0.012f || Mathf.Sign(prediction) != Mathf.Sign(truth)) continue;
                shares[i]++; shares[j]++;
            }
            int cumulative = 0, awarded = 0;
            for (int i = 0; i < result.Length; i++)
            {
                cumulative += shares[i];
                int subtotal = score.total == 0
                    ? Mathf.RoundToInt(100f * (i + 1) / result.Length)
                    : Mathf.RoundToInt(50f * cumulative / score.total);
                result[i] = subtotal - awarded;
                awarded = subtotal;
            }
            return result;
        }

        public static int[] ClockwiseOrder(Vector2[] positions)
        {
            var order=new int[positions.Length];
            for(int i=0;i<order.Length;i++)order[i]=i;
            Array.Sort(order,(a,b)=>
            {
                // UI coordinates point down: twelve o'clock is zero, right is 90.
                float aa=Mathf.Repeat(Mathf.Atan2(positions[a].x,-positions[a].y)*Mathf.Rad2Deg,360);
                float bb=Mathf.Repeat(Mathf.Atan2(positions[b].x,-positions[b].y)*Mathf.Rad2Deg,360);
                int comparison=aa.CompareTo(bb);return comparison==0?a.CompareTo(b):comparison;
            });
            return order;
        }

        public static float ReferenceRadius(Puzzle puzzle, int index)
        {
            float rankSum = 0;
            int count = 0;
            for (int i = 0; i < puzzle.topics.Length; i++)
            {
                if (Mathf.Abs(puzzle.topics[i].similarity - puzzle.topics[index].similarity) > puzzle.tieTolerance) continue;
                int rank = 0;
                for (int j = 0; j < puzzle.topics.Length; j++)
                    if (puzzle.topics[j].similarity > puzzle.topics[i].similarity) rank++;
                rankSum += rank;
                count++;
            }
            return Mathf.Lerp(0.27f, 0.86f, rankSum / Mathf.Max(1, count) / Mathf.Max(1f, puzzle.topics.Length - 1f));
        }
    }
}
