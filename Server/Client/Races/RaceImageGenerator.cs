using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.IO;
using System.Collections.Generic;
using System.Linq;

namespace Server.Client.Races
{
    public static class RaceImageGenerator
    {
        // Path to the template relative to the server executable
        // Ensure "Server/Resources/Images/race_template.png" exists or is copied to output
        private static readonly string TemplatePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "Images", "race_template.png");
        
        // Font Configuration
        private static readonly string FontName = "Bahnschrift"; // Looks similar to the sample, standard on Windows
        private static readonly float FontSize = 28f; 
        
        // Grid Configuration (Estimated for ~1000x1500 image)
        // Hardcoded Y-coordinates for each row to handle non-uniform spacing in the image
        private static readonly int[] RowYCoordinates = new int[] 
        { 
            442, // Row 1 
            537, // Row 2 
            627, // Row 3
            713, // Row 4
            799, // Row 5
            885, // Row 6
            971, // Row 7
            1057, // Row 8
            1143, // Row 9
            1229  // Row 10
        };
        
        // Horizontal centers for each column
        private const int ColNameX = 295;    // Center X for Name column
        private const int ColWagerX = 540;   // Center X for Wager column
        private const int ColPrizeX = 840;   // Center X for Prize column

        public static Stream GenerateLeaderboardImage(List<RaceParticipant> participants, List<RacePrize> prizes)
        {
            if (!File.Exists(TemplatePath))
            {
                Console.WriteLine($"[RaceImageGenerator] Template missing at: {TemplatePath}");
                return null;
            }

            // Load the image
            Bitmap bitmap; 
            try 
            {
                bitmap = new Bitmap(TemplatePath);
            }
            catch(Exception ex)
            {
                Console.WriteLine($"[RaceImageGenerator] Failed to load bitmap: {ex.Message}");
                return null;
            }

            using (var g = Graphics.FromImage(bitmap))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;

                // Create fonts and brushes
                // Fallback to Arial if Bahnschrift isn't found, though it's standard on Win10+
                Font font;
                try { font = new Font(FontName, FontSize, FontStyle.Bold); }
                catch { font = new Font("Arial", FontSize, FontStyle.Bold); }

                using (font)
                using (var brushOrange = new SolidBrush(Color.FromArgb(255, 204, 102))) // Matches the "1500M GP" color
                using (var brushWhite = new SolidBrush(Color.FromArgb(230, 230, 230)))   // Off-white for names
                {
                    for (int i = 0; i < 10; i++)
                    {
                        // Use the manual array if index is within bounds, otherwise fallback to calculation (though loop is < 10)
                        int y = (i < RowYCoordinates.Length) ? RowYCoordinates[i] : 455 + (i * 89);
                        
                        int rank = i + 1;

                        // 1. Get Data
                        var participant = i < participants.Count ? participants[i] : null;
                        var prizeObj = prizes.FirstOrDefault(p => p.Rank == rank);
                        
                        string nameText = participant != null ? participant.Username : "";
                        string wagerText = participant != null ? Server.Client.Utils.GpFormatter.Format(participant.TotalWagered) : "";
                        string prizeText = prizeObj != null ? prizeObj.Prize : "";
                        
                        // Remove GP from prize text if it exists (user requested no GP displayed)
                        if (!string.IsNullOrEmpty(prizeText))
                        {
                             prizeText = prizeText.Replace("GP", "", StringComparison.OrdinalIgnoreCase).Trim();
                        }

                        // 2. Truncate long names
                        if (nameText.Length > 14) nameText = nameText.Substring(0, 12) + "..";

                        // 3. Draw Text
                        // Name: White
                        DrawCenteredText(g, nameText, font, brushWhite, ColNameX, y);
                        
                        // Wager: Orange/Gold
                        DrawCenteredText(g, wagerText, font, brushOrange, ColWagerX, y);

                        // Prize: Orange/Gold
                        DrawCenteredText(g, prizeText, font, brushOrange, ColPrizeX, y);
                    }
                }
            }

            // Save to stream
            var ms = new MemoryStream();
            bitmap.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
            ms.Position = 0;
            return ms;
        }

        private static void DrawCenteredText(Graphics g, string text, Font font, Brush brush, float centerX, float y)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            
            var size = g.MeasureString(text, font);
            float x = centerX - (size.Width / 2);
            // Center vertically based on measurement? 
            // Usually StartY is tuned to be the 'top' padding. 
            // If StartY is the visual center of the row, use: float drawY = y - (size.Height / 2);
            // Let's assume StartY is calibrated to the top-ish of the text.
            
            g.DrawString(text, font, brush, x, y);
        }
    }
}
