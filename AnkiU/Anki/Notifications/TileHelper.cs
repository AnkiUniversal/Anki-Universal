/*
Copyright (C) 2016-2017 Anki Universal Team <ankiuniversal@outlook.com>

This program is free software: you can redistribute it and/or modify
it under the terms of the GNU Affero General Public License as
published by the Free Software Foundation, either version 3 of the
License, or (at your option) any later version.

This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU Affero General Public License for more details.

You should have received a copy of the GNU Affero General Public License
along with this program.  If not, see <http://www.gnu.org/licenses/>.
*/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Data.Xml.Dom;
using Windows.UI;
using Windows.UI.Notifications;
using Windows.UI.Popups;
using Windows.UI.StartScreen;

namespace AnkiU.Anki.Notifications
{
    public class TilesHelper
    {
        private static readonly string XML_TEMPLATE = $@"
                                                <tile version='3'>
                                                    <visual branding='nameAndLogo'>
                                                        <binding template='TileMedium'>
                                                            <text hint-wrap='true'>New Cards</text>
                                                            <text hint-wrap='true' hint-style='captionSubtle'>NewNumber</text>
                                                            <text hint-wrap='true'>Due Cards</text>
                                                            <text hint-wrap='true' hint-style='captionSubtle'>DueNumber</text>   
                                                        </binding>
                                                        <binding template='TileWide'>
                                                            <text hint-wrap='true'>New Cards</text>
                                                            <text hint-wrap='true' hint-style='captionSubtle'>NewNumber</text>
                                                            <text hint-wrap='true'>Due Cards</text>
                                                            <text hint-wrap='true' hint-style='captionSubtle'>DueNumber</text>   
                                                        </binding>
                                                        <binding template='TileLarge'>
                                                            <text hint-wrap='true'>New Cards</text>
                                                            <text hint-wrap='true' hint-style='captionSubtle'>NewNumber</text>
                                                            <text hint-wrap='true'>Due Cards</text>
                                                            <text hint-wrap='true' hint-style='captionSubtle'>DueNumber</text>   
                                                        </binding>
                                                    </visual>
                                                </tile>";

        public static void SendPrimaryTileNoficiation(string newCards, string dueCards)
        {
            XmlDocument doc = new XmlDocument();
            doc.LoadXml(XML_TEMPLATE);

            UpdateXmlNewDueCards(newCards, dueCards, doc);
            
            TileNotification notification = new TileNotification(doc);
            TileUpdateManager.CreateTileUpdaterForApplication().Update(notification);
        }

        public static void SendSecondaryTileNotification(string tileId, string newCards, string dueCards)
        {
            XmlDocument doc = new XmlDocument();
            doc.LoadXml(XML_TEMPLATE);

            UpdateXmlNewDueCards(newCards, dueCards, doc);

            TileNotification notification = new TileNotification(doc);
            TileUpdateManager.CreateTileUpdaterForSecondaryTile(tileId).Update(notification);
        }

        public static async Task<IReadOnlyList<SecondaryTile>> FindAllSecondaryTilesAsync()
        {
            return await SecondaryTile.FindAllAsync();
        }

        public static async Task UpdateTile(string tileId, string newCards, string dueCards)
        {
            var tile = await FindExisting(tileId);
            if (tile != null)
            {                
                SendSecondaryTileNotification(tileId, newCards, dueCards);
            }
        }

        public static async Task RemoveTile(string tileId)
        {
            var tile = await FindExisting(tileId);
            if(tile != null)
                await tile.RequestDeleteAsync();            
        }

        private static void UpdateXmlNewDueCards(string newCards, string dueCards, XmlDocument doc)
        {
            foreach (XmlElement textEl in doc.SelectNodes("//text").OfType<XmlElement>())
            {
                if (textEl.InnerText.Equals("NewNumber"))
                    textEl.InnerText = newCards;
                else if (textEl.InnerText.Equals("DueNumber"))
                    textEl.InnerText = dueCards;
            }
        }

        public static async Task<SecondaryTile> PinNewSecondaryTile()
        {
            SecondaryTile tile = GenerateSecondaryTile("Secondary tile");

            await tile.RequestCreateAsync();

            return tile;
        }

        public static SecondaryTile GenerateSecondaryTile(string tileId, string displayName)
        {
            SecondaryTile tile = new SecondaryTile(tileId, displayName, "args", new Uri("ms-appx:///Assets/Square150x150Logo.png"), TileSize.Default);
            tile.VisualElements.Square71x71Logo = new Uri("ms-appx:///Assets/Square71x71Logo.png");
            tile.VisualElements.Wide310x150Logo = new Uri("ms-appx:///Assets/Wide310x150Logo.png");
            tile.VisualElements.Square310x310Logo = new Uri("ms-appx:///Assets/Square310x310Logo.png");
            tile.VisualElements.Square44x44Logo = new Uri("ms-appx:///Assets/Square44x44Logo.png"); // Branding logo

            return tile;
        }

        public static SecondaryTile GenerateSecondaryTile(string displayName)
        {
            return GenerateSecondaryTile(DateTime.Now.Ticks.ToString(), displayName);
        }

        public static async Task<SecondaryTile> FindExisting(string tileId)
        {
            return (await SecondaryTile.FindAllAsync()).FirstOrDefault(i => i.TileId.Equals(tileId));
        }

        public static async Task<SecondaryTile> PinNewSecondaryTile(string displayName, string xml)
        {
            SecondaryTile tile = GenerateSecondaryTile(displayName);
            await tile.RequestCreateAsync();

            XmlDocument doc = new XmlDocument();
            doc.LoadXml(xml);

            TileUpdateManager.CreateTileUpdaterForSecondaryTile(tile.TileId).Update(new TileNotification(doc));

            return tile;
        }

        public static async Task UpdateTiles(string xml)
        {
            XmlDocument doc;

            try
            {
                doc = new XmlDocument();
                doc.LoadXml(xml);
            }

            catch (Exception ex)
            {
                await new MessageDialog(ex.ToString(), "ERROR: Invalid XML").ShowAsync();
                return;
            }

            await UpdateTiles(doc);
        }

        public static async Task UpdateTiles(XmlDocument doc)
        {
            try
            {
                TileUpdateManager.CreateTileUpdaterForApplication().Update(new TileNotification(doc));

                await UpdateTile("Small", doc);
                await UpdateTile("Medium", doc);
                await UpdateTile("Wide", doc);
                await UpdateTile("Large", doc);
            }

            catch (Exception ex)
            {
                await new MessageDialog(ex.ToString(), "ERROR: Failed updating tiles").ShowAsync();
            }
        }

        public static async Task UpdateTile(string tileId, XmlDocument doc)
        {
            if (!SecondaryTile.Exists(tileId))
            {
                SecondaryTile tile = GenerateSecondaryTile(tileId, tileId);
                tile.VisualElements.ShowNameOnSquare310x310Logo = true;
                await tile.RequestCreateAsync();
            }

            TileUpdateManager.CreateTileUpdaterForSecondaryTile(tileId).Update(new TileNotification(doc));
        }
    }
}
