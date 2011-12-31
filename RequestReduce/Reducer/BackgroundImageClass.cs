﻿using System;
using System.Linq;
using System.Text.RegularExpressions;
using RequestReduce.Utilities;

namespace RequestReduce.Reducer
{
    public enum RepeatStyle
    {
        Repeat,
        NoRepeat,
        XRepeat,
        YRepeat
    }

    public enum PositionMode
    {
        Percent,
        Unit,
        Direction,
    }

    public enum Direction
    {
        Center,
        Left,
        Right,
        Top,
        Bottom
    }

    [Flags]
    public enum PropertyCompletion
    {
        HasNothing = 0,
        HasImage = 1,
        HasRepeat = 2,
        HasXOffset = 4,
        HasYOffset = 8,
        HasWidth = 16,
        HasHeight = 32,
        HasPaddingLeft = 64,
        HasPaddingRight = 128,
        HasPaddingTop = 256,
        HasPaddingBottom = 512
    }

    public class BackgroundImageClass
    {
        private static readonly RegexCache Regex = new RegexCache();

        public BackgroundImageClass(string originalClassString)
        {
            var selectorIdx = originalClassString.IndexOf("{", StringComparison.Ordinal);
            if (selectorIdx == -1) selectorIdx = 0;
            OriginalClassString = originalClassString.Substring(selectorIdx);
            Selector = originalClassString.Substring(0, selectorIdx).Trim();
            var match = Regex.ImageUrlPattern.Match(originalClassString);
            if (match.Success)
            {
                PropertyCompletion = PropertyCompletion | PropertyCompletion.HasImage;
                var imageUrl = match.Groups["url"].Value.Replace("'", "").Replace("\"", "").Trim();
                if (imageUrl.Length > 0)
                    ImageUrl = imageUrl;
            }
            var repeatMatch = Regex.RepeatPattern.Matches(originalClassString);
            if(repeatMatch.Count > 0)
            {
                PropertyCompletion = PropertyCompletion | PropertyCompletion.HasRepeat;
                Repeat = (RepeatStyle) Enum.Parse(typeof(RepeatStyle), repeatMatch[repeatMatch.Count-1].Value.Replace("-",""), true);
            }
            var widthMatch = Regex.WidthPattern.Matches(originalClassString);
            if (widthMatch.Count > 0)
            {
                PropertyCompletion = PropertyCompletion | PropertyCompletion.HasWidth;
                ExplicitWidth = Int32.Parse(widthMatch[widthMatch.Count - 1].Groups["width"].Value);
            }

            var heightMatch = Regex.HeightPattern.Matches(originalClassString);
            if (heightMatch.Count > 0)
            {
                PropertyCompletion = PropertyCompletion | PropertyCompletion.HasHeight;
                ExplicitHeight = Int32.Parse(heightMatch[heightMatch.Count - 1].Groups["height"].Value);
            }

            var paddingMatches = Regex.PaddingPattern.Matches(originalClassString);
            if (paddingMatches.Count > 0)
            {
                foreach (var pads in from Match paddingMatch in paddingMatches select GetPadding(paddingMatch))
                {
                    if (pads[0] != null)
                    {
                        PropertyCompletion = PropertyCompletion | PropertyCompletion.HasPaddingTop;
                        PaddingTop = pads[0];
                    }
                    if (pads[1] != null)
                    {
                        PropertyCompletion = PropertyCompletion | PropertyCompletion.HasPaddingLeft;
                        PaddingLeft = pads[1];
                    }
                    if (pads[2] != null)
                    {
                        PropertyCompletion = PropertyCompletion | PropertyCompletion.HasPaddingBottom;
                        PaddingBottom = pads[2];
                    }
                    if (pads[3] != null)
                    {
                        PropertyCompletion = PropertyCompletion | PropertyCompletion.HasPaddingRight;
                        PaddingRight = pads[3];
                    }
                }
            }

            var offsetMatches = Regex.OffsetPattern.Matches(originalClassString);
            if (offsetMatches.Count > 0)
            {
                foreach (Match offsetMatch in offsetMatches)
                    SetOffsets(offsetMatch);
            }
            if (XOffset.PositionMode == PositionMode.Direction && (PropertyCompletion & PropertyCompletion.HasYOffset) != PropertyCompletion.HasYOffset)
                YOffset = new Position {PositionMode = PositionMode.Direction};
            if (YOffset.PositionMode == PositionMode.Direction && (PropertyCompletion & PropertyCompletion.HasXOffset) != PropertyCompletion.HasXOffset)
                XOffset = new Position { PositionMode = PositionMode.Direction };
        }

        private int?[] GetPadding(Match paddingMatch)
        {
            var padVals = new int?[4];
            var side = paddingMatch.Groups["side"].Value.ToLower();
            var pad1 = paddingMatch.Groups["pad1"].Value;
            var pad2 = paddingMatch.Groups["pad2"].Value;
            var pad3 = paddingMatch.Groups["pad3"].Value;
            var pad4 = paddingMatch.Groups["pad4"].Value;

            if (side == "-left")
                padVals[3] = calcPadPixels(pad1, ExplicitWidth);
            else if (side == "-right")
                padVals[1] = calcPadPixels(pad1, ExplicitWidth);
            else if (side == "-top")
                padVals[0] = calcPadPixels(pad1, Height);
            else if (side == "-bottom")
                padVals[2] = calcPadPixels(pad1, Height);
            else
            {
                var groupCount = paddingMatch.Groups.Cast<Group>().Count(x => x.Length > 0);
                switch (groupCount-1)
                {
                    case 1:
                        padVals[0] = calcPadPixels(pad1, ExplicitHeight);
                        padVals[1] = calcPadPixels(pad1, ExplicitWidth);
                        padVals[2] = calcPadPixels(pad1, ExplicitHeight);
                        padVals[3] = calcPadPixels(pad1, ExplicitWidth);
                        break;
                    case 2:
                        padVals[0] = calcPadPixels(pad1, ExplicitHeight);
                        padVals[1] = calcPadPixels(pad2, ExplicitWidth);
                        padVals[2] = calcPadPixels(pad1, ExplicitHeight);
                        padVals[3] = calcPadPixels(pad2, ExplicitWidth);
                        break;
                    case 3:
                        padVals[0] = calcPadPixels(pad1, ExplicitHeight);
                        padVals[1] = calcPadPixels(pad2, ExplicitWidth);
                        padVals[2] = calcPadPixels(pad3, ExplicitHeight);
                        padVals[3] = calcPadPixels(pad2, ExplicitWidth);
                        break;
                    case 4:
                        padVals[0] = calcPadPixels(pad1, ExplicitHeight);
                        padVals[1] = calcPadPixels(pad2, ExplicitWidth);
                        padVals[2] = calcPadPixels(pad3, ExplicitHeight);
                        padVals[3] = calcPadPixels(pad4, ExplicitWidth);
                        break;
                }
            }
            return padVals;
        }

        private int calcPadPixels(string pad, int? pixels)
        {
            var result = 0;
            if (pixels == null) return result;

            pad = pad.ToLower();
            if (pad.EndsWith("%"))
            {
                int.TryParse(pad.Replace("%", ""), out result);
                result = (int)((result/100f)*(int)pixels);
            }
            else if (pad.EndsWith("px"))
                int.TryParse(pad.Replace("px", ""), out result);
            else
                if(!int.TryParse(pad, out result))
                    result = -1;

            return result;
        }

        private void SetOffsets(Match offsetMatch)
        {
            var offset1 = offsetMatch.Groups["offset1"].Value.ToLower();
            var offset2 = offsetMatch.Groups["offset2"].Value.ToLower();
            bool flip = offset1 == "top" || offset1 == "bottom" || offset2 == "right" || offset2 == "left";
            var offset1Position = ",top,bottom,right,left,center".IndexOf(offset1, StringComparison.Ordinal) > 0
                                      ? ParseStringOffset(offset1)
                                      : ParseNumericOffset(offset1);
            var offset2Position = ",top,bottom,right,left,center".IndexOf(offset2, StringComparison.Ordinal) > 0
                                      ? ParseStringOffset(offset2)
                                      : ParseNumericOffset(offset2);
            if (flip)
            {
                if(offset2Position.PositionMode != PositionMode.Percent || offset2Position.Offset > 0) XOffset = offset2Position;
                if(offset1Position.PositionMode != PositionMode.Percent || offset1Position.Offset > 0) YOffset = offset1Position;
                if (offset1.Length > 0) PropertyCompletion = PropertyCompletion | PropertyCompletion.HasYOffset;
                if (offset2.Length > 0) PropertyCompletion = PropertyCompletion | PropertyCompletion.HasXOffset;
            }
            else
            {
                if(offset1Position.PositionMode != PositionMode.Percent || offset1Position.Offset > 0) XOffset = offset1Position;
                if(offset2Position.PositionMode != PositionMode.Percent || offset2Position.Offset > 0) YOffset = offset2Position;
                if (offset1.Length > 0) PropertyCompletion = PropertyCompletion | PropertyCompletion.HasXOffset;
                if (offset2.Length > 0) PropertyCompletion = PropertyCompletion | PropertyCompletion.HasYOffset;
            }

            Important = offsetMatch.ToString().ToLower().Contains("!important");

        }

        private Position ParseStringOffset(string offsetString)
        {
            return new Position
            {
                PositionMode = PositionMode.Direction,
                Direction = (Direction) Enum.Parse(typeof (Direction), offsetString, true)
            };
        }

        private Position ParseNumericOffset(string offsetString)
        {
            var offset = new Position();
            if (string.IsNullOrEmpty(offsetString))
                return offset;
            if (offsetString.Length > 2 && "|in|cm|mm|em|ex|pt|pc".IndexOf(offsetString.Substring(offsetString.Length-2,2), StringComparison.Ordinal) > -1)
                return offset;
            var trim = 0;
            if (offsetString.EndsWith("%"))
            {
                trim = 1;
                offset.PositionMode = PositionMode.Percent;
            }
            else 
                offset.PositionMode = PositionMode.Unit;
            if (offsetString.EndsWith("px"))
                trim = 2;
            int units;
            Int32.TryParse(offsetString.Substring(0, offsetString.Length - trim), out units);
            offset.Offset = units;
            return offset;
        }

        public string OriginalClassString { get; set; }
        public string Selector { get; set; }
        public string ImageUrl { get; set; }
        public RepeatStyle Repeat { get; set; }
        public Position XOffset { get; set; }
        public Position YOffset { get; set; }
        public int? Width 
        { 
            get
            {
                if (PaddingLeft < 0 || PaddingRight < 0)
                    return null;
                var computedWidth = ExplicitWidth + (PaddingLeft ?? 0) + (PaddingRight ?? 0);
                return computedWidth == 0 ? (int?) null : computedWidth;
            }
        }
        public int? Height 
        {
            get
            {
                if (PaddingTop < 0 || PaddingBottom < 0)
                    return null;
                var computedHeight = ExplicitHeight + (PaddingTop ?? 0) + (PaddingBottom ?? 0);
                return computedHeight == 0 ? (int?)null : computedHeight;
            }
        }
        public int ExplicitWidth { get; set; }
        public int ExplicitHeight { get; set; }
        public bool Important { get; set; }
        public bool HasAllProperties { get; set; }
        public int? PaddingLeft { get; set; }
        public int? PaddingRight { get; set; }
        public int? PaddingTop { get; set; }
        public int? PaddingBottom { get; set; }
        public PropertyCompletion PropertyCompletion { get; set; }
    }
}
