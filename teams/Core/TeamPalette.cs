using UnityEngine;

namespace SpiderTeams
{
    public static class TeamPalette
    {
        private static readonly Color[] _named = ParseNamed();

        public static Color TeamColor(int team)
        {
            if (team < 0)
            {
                return Color.white;
            }
            if (team < _named.Length)
            {
                return _named[team];
            }

            // Golden-ratio hue steps keep generated teams far apart on the color wheel.
            float hue = Mathf.Repeat(Consts.Palette.GeneratedHueStart + team * Consts.Palette.GoldenRatio, 1f);
            float saturation = 0.6f + 0.15f * (team % 3);
            float value = (team % 2 == 0) ? 1f : 0.85f;
            return Color.HSVToRGB(hue, saturation, value);
        }

        public static string TeamName(int team)
        {
            if (team >= 0 && team < Consts.Palette.NamedTeams.Length)
            {
                return Consts.Palette.NamedTeams[team] + " Team";
            }
            return "Team " + (team + 1);
        }

        // Rank 0 keeps the team color; odd ranks get darker and even ranks lighter, one step per pair.
        public static Color MemberColor(int team, int rank)
        {
            Color baseColor = TeamColor(team);
            if (rank <= 0)
            {
                return baseColor;
            }

            float h, s, v;
            Color.RGBToHSV(baseColor, out h, out s, out v);
            float amount = Mathf.Min(Consts.Palette.ShadeStep * ((rank + 1) / 2), Consts.Palette.MaxShade);
            if (rank % 2 == 1)
            {
                v = Mathf.Clamp01(v - amount);
            }
            else
            {
                s = Mathf.Clamp01(s - amount);
                v = Mathf.Clamp01(v + amount * 0.5f);
            }
            return Color.HSVToRGB(h, s, v);
        }

        public static string Hex(Color color)
        {
            return ColorUtility.ToHtmlStringRGB(color);
        }

        private static Color[] ParseNamed()
        {
            var colors = new Color[Consts.Palette.NamedColors.Length];
            for (int i = 0; i < colors.Length; i++)
            {
                Color parsed;
                colors[i] = ColorUtility.TryParseHtmlString(Consts.Palette.NamedColors[i], out parsed) ? parsed : Color.white;
            }
            return colors;
        }
    }
}
