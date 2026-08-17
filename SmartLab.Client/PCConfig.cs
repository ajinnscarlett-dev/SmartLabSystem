using System;
using System.Text.RegularExpressions;

namespace SmartLab.Client
{
    public static class PCConfig
    {
        // ==========================================
        // AUTO-DETECTED COMPUTER IDENTITY
        // ==========================================
        //
        // Supported Windows computer-name examples:
        //
        // 601-PC01  -> 601-PC01
        // PC01      -> 601-PC01 (temporary/default lab)
        // PC02      -> 601-PC02
        //
        // For names that already contain a lab number:
        //
        // 602-PC01  -> 602-PC01
        // 603-PC12  -> 603-PC12
        //
        // Unknown names are NOT guessed. The actual
        // Windows machine name is returned instead.
        // ==========================================

        private const string DefaultLaboratory = "601";

        public static string ComputerName =>
            Environment.MachineName.Trim();

        public static string PCNumber =>
            GetSmartLabPcNumber(ComputerName);

        public static string GetSmartLabPcNumber(
            string machineName)
        {
            string name =
                (machineName ?? string.Empty).Trim();

            if (string.IsNullOrWhiteSpace(name))
            {
                return "UNKNOWN-PC";
            }

            // ------------------------------------------
            // SMARTLAB FORMAT:
            // 601-PC01, 602-PC12, etc.
            // ------------------------------------------

            Match labPcMatch = Regex.Match(
                name,
                @"^(?<lab>\d{3})-PC(?<pc>\d{1,3})$",
                RegexOptions.IgnoreCase);

            if (labPcMatch.Success)
            {
                string lab =
                    labPcMatch.Groups["lab"].Value;

                if (int.TryParse(
                    labPcMatch.Groups["pc"].Value,
                    out int pcNumber))
                {
                    return $"{lab}-PC{pcNumber:00}";
                }
            }

            // ------------------------------------------
            // SIMPLE PC FORMAT:
            // PC01, PC2, PC12
            // ------------------------------------------

            Match simplePcMatch = Regex.Match(
                name,
                @"^PC(?<pc>\d{1,3})$",
                RegexOptions.IgnoreCase);

            if (simplePcMatch.Success &&
                int.TryParse(
                    simplePcMatch.Groups["pc"].Value,
                    out int simplePcNumber))
            {
                return
                    $"{DefaultLaboratory}-PC{simplePcNumber:00}";
            }

            // ------------------------------------------
            // COMMON LAB PREFIX FORMAT:
            // LAB601-PC01
            // COMLAB601-PC01
            // LAB-601-PC01
            // ------------------------------------------

            Match prefixedPcMatch = Regex.Match(
                name,
                @"^(?:LAB|COMLAB)-?(?<lab>\d{3})-?PC(?<pc>\d{1,3})$",
                RegexOptions.IgnoreCase);

            if (prefixedPcMatch.Success)
            {
                string lab =
                    prefixedPcMatch.Groups["lab"].Value;

                if (int.TryParse(
                    prefixedPcMatch.Groups["pc"].Value,
                    out int prefixedPcNumber))
                {
                    return
                        $"{lab}-PC{prefixedPcNumber:00}";
                }
            }

            // ------------------------------------------
            // UNKNOWN FORMAT
            // ------------------------------------------
            //
            // Never invent a PC number. Return the
            // actual Windows computer name instead.
            // ------------------------------------------

            return name.ToUpperInvariant();
        }
    }
}
