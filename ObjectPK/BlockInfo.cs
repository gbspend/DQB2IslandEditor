using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Media;

namespace DQB2IslandEditor.ObjectPK
{
    public class BlockInfo : ObjectInfo
    {
        //Liquid water blocks (clear/muddy/hot/sea, any shape or variant). Not "Water Sigil Block".
        private static readonly Regex WATER_NAME = new Regex(@"water (full|surface|shallow|small) block", RegexOptions.IgnoreCase);
        public readonly bool isWater;

        public BlockInfo(ushort objectId, short imageId, byte tab, Colour colour, byte hardness, string name)
            : base(objectId, imageId, tab, colour, hardness, name)
        {
            //Checked against the default name, since the extra data file can rename blocks.
            isWater = WATER_NAME.IsMatch(name);
        }
    }
}
