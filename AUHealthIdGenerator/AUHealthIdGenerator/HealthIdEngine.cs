using System;
using System.Collections.Generic;
using System.Text;

namespace AUHealthIdGenerator
{
    /// <summary>
    /// Identifier type enum for all supported Australian health identifier types.
    /// </summary>
    public enum HealthIdType
    {
        HPI_I,          // Healthcare Provider Identifier – Individual (16-digit)
        HPI_O,          // Healthcare Provider Identifier – Organisation (16-digit)
        IHI,            // Individual Healthcare Identifier (16-digit)
        Medicare,       // Medicare Card Number (10-digit + line number)
        ProviderNumber  // Medicare Provider Number (6 digits + 1 alpha + 1 check alpha)
    }

    /// <summary>
    /// Controls how Provider Numbers are generated.
    /// </summary>
    public enum ProviderNumberMode
    {
        Random,         // Any structurally valid number
        StateSpecific   // Uses realistic state-based numeric prefixes
    }

    /// <summary>
    /// Result returned from a validation operation.
    /// </summary>
    public class ValidationResult
    {
        public bool IsValid { get; set; }
        public string InputValue { get; set; } = string.Empty;
        public HealthIdType? DetectedType { get; set; }
        public string TypeLabel { get; set; } = string.Empty;
        public string FormatDescription { get; set; } = string.Empty;
        public string CheckDigitStatus { get; set; } = string.Empty;
        public string PrefixStatus { get; set; } = string.Empty;
        public string LengthStatus { get; set; } = string.Empty;
        public List<string> Errors { get; set; } = new();
        public List<string> Details { get; set; } = new();
    }

    /// <summary>
    /// Core engine for generating and validating Australian health identifiers.
    ///
    /// References:
    ///   - HI Service Operator Technical Specifications (ADHA)
    ///   - Medicare number format (Services Australia)
    ///   - Medicare Provider Number format (Services Australia / MBS)
    ///   - Luhn algorithm (ISO/IEC 7812)
    /// </summary>
    public static class HealthIdEngine
    {
        private static readonly Random _rng = new();

        // ─── HPI / IHI Prefixes ──────────────────────────────────────────────────
        private const string HpiiPrefix = "800361";
        private const string HpioPrefix = "800362";
        private const string IhiPrefix  = "800360";

        // ─── Medicare card issuer digits ─────────────────────────────────────────
        // 2=NSW/ACT  3=VIC/TAS  4=QLD  5=SA/NT  6=WA
        private static readonly int[] MedicareIssuers = { 2, 3, 4, 5, 6 };

        // ─── Provider Number ─────────────────────────────────────────────────────
        // Format:  NNNNNN PLC C
        //   NNNNNN = 6-digit provider stem
        //   PLC    = Practice Location Character (one alphanumeric, NOT I, O or S)
        //   C      = Check Character (one alpha, computed via weighted mod-11)
        //
        // Algorithm (Services Australia / Claiming API spec):
        //   sum = d1*3 + d2*5 + d3*8 + d4*4 + d5*2 + d6*1 + PLV*6
        //   remainder = sum mod 11
        //   check char = CheckTable[remainder]   (table has 11 entries, remainder 0–10)
        //
        // PLV table: 0-9 → 0-9, A→10, B→11, C→12, D→13, E→14, F→15, G→16,
        //            H→17, J→18, K→19, L→20, M→21, N→22, P→23, Q→24, R→25,
        //            T→26, U→27, V→28, W→29, X→30, Y→31
        //            (I, O, S are NOT valid PLCs)
        //
        // Check char table: 0→Y, 1→X, 2→W, 3→T, 4→L, 5→K, 6→J, 7→H, 8→F, 9→B, 10→A
        private const string CheckTable = "YXWTLKJHFBA";  // index 0–10

        // Valid PLC characters in order (no I, O, S)
        private const string ValidPlcChars = "0123456789ABCDEFGHJKLMNPQRTUVWXY";

        // PLV lookup: maps each valid PLC char to its numeric Practice Location Value
        private static readonly Dictionary<char, int> PlvTable = BuildPlvTable();

        private static Dictionary<char, int> BuildPlvTable()
        {
            var t = new Dictionary<char, int>();
            for (int i = 0; i < ValidPlcChars.Length; i++)
                t[ValidPlcChars[i]] = i;
            return t;
        }

        // For generation we use only alpha PLCs (common in practice)
        private static readonly char[] AlphaPlcChars =
            ValidPlcChars.Where(char.IsLetter).ToArray();

        // State-based numeric prefix ranges (first 4 digits of the 6-digit number).
        // These are illustrative realistic ranges used by Services Australia.
        // Format: (state label, low, high) — we pick a random value in [low, high]
        private static readonly (string State, int Low, int High)[] StatePrefixes =
        {
            ("NSW/ACT", 2000, 2999),
            ("VIC/TAS", 3000, 3999),
            ("QLD",     4000, 4999),
            ("SA/NT",   5000, 5999),
            ("WA",      6000, 6999),
        };

        // ─── Generation ──────────────────────────────────────────────────────────

        public static List<string> Generate(HealthIdType type, int count,
            ProviderNumberMode providerMode = ProviderNumberMode.Random,
            string? providerState = null)
        {
            var results = new List<string>(count);
            for (int i = 0; i < count; i++)
            {
                results.Add(type switch
                {
                    HealthIdType.HPI_I          => GenerateHpi(HpiiPrefix),
                    HealthIdType.HPI_O          => GenerateHpi(HpioPrefix),
                    HealthIdType.IHI            => GenerateHpi(IhiPrefix),
                    HealthIdType.Medicare       => GenerateMedicare(),
                    HealthIdType.ProviderNumber => GenerateProviderNumber(providerMode, providerState),
                    _ => throw new ArgumentOutOfRangeException(nameof(type))
                });
            }
            return results;
        }

        // ── HPI-I / HPI-O / IHI ──────────────────────────────────────────────────

        private static string GenerateHpi(string prefix)
        {
            var sb = new StringBuilder(prefix);
            for (int i = 0; i < 9; i++)
                sb.Append(_rng.Next(0, 10));
            string payload = sb.ToString();
            return payload + LuhnCheckDigit(payload);
        }

        // ── Medicare card ─────────────────────────────────────────────────────────
        // Format: D XXXXXXX C I
        //   D        = issuer digit (position 1)
        //   XXXXXXX  = 7 random digits (positions 2–8)
        //   C        = check digit (position 9) — weighted mod-10 over positions 1–8
        //   I        = issue/IRN number (position 10, 1–9, after "/")

        private static string GenerateMedicare()
        {
            int issuer = MedicareIssuers[_rng.Next(MedicareIssuers.Length)];
            var body = new StringBuilder();
            for (int i = 0; i < 7; i++)
                body.Append(_rng.Next(0, 10));
            string payload = issuer + body.ToString(); // 8 digits — positions 1–8
            int check = MedicareLuhnCheck(payload);    // check digit = position 9
            int line  = _rng.Next(1, 10);              // IRN = position 10
            return $"{issuer}{body}{check}/{line}";
        }

        // ── Medicare Provider Number ──────────────────────────────────────────────

        /// <summary>
        /// Generates a structurally valid Medicare Provider Number.
        /// Format: NNNNNN PLC C  (8 characters total)
        ///   NNNNNN = 6-digit provider stem
        ///   PLC    = Practice Location Character (alphanumeric, not I/O/S)
        ///   C      = Check Character (weighted mod-11, maps to YXWTLKJHFBA)
        /// </summary>
        public static string GenerateProviderNumber(
            ProviderNumberMode mode = ProviderNumberMode.Random,
            string? targetState = null)
        {
            string digits;

            if (mode == ProviderNumberMode.StateSpecific)
            {
                (string State, int Low, int High) range;
                if (!string.IsNullOrEmpty(targetState))
                {
                    range = Array.Find(StatePrefixes,
                        p => p.State.Contains(targetState, StringComparison.OrdinalIgnoreCase));
                    if (range == default)
                        range = StatePrefixes[_rng.Next(StatePrefixes.Length)];
                }
                else
                {
                    range = StatePrefixes[_rng.Next(StatePrefixes.Length)];
                }
                int prefix4 = _rng.Next(range.Low, range.High + 1);
                int suffix2 = _rng.Next(0, 100);
                digits = $"{prefix4:D4}{suffix2:D2}";
            }
            else
            {
                digits = _rng.Next(100000, 1000000).ToString("D6");
            }

            // Pick a random alpha PLC (digits are also valid but alpha is most common in practice)
            char plc = AlphaPlcChars[_rng.Next(AlphaPlcChars.Length)];
            char check = ProviderNumberCheckChar(digits, plc);
            return $"{digits}{plc}{check}";
        }

        /// <summary>
        /// Computes the Provider Number check character using the official Services Australia algorithm.
        /// sum = d1*3 + d2*5 + d3*8 + d4*4 + d5*2 + d6*1 + PLV*6
        /// check = CheckTable[sum mod 11]   where CheckTable = "YXWTLKJHFBA"
        /// </summary>
        public static char ProviderNumberCheckChar(string digits6, char plc)
        {
            if (digits6.Length != 6) throw new ArgumentException("Must be 6 digits", nameof(digits6));
            char upperPlc = char.ToUpper(plc);
            if (!PlvTable.TryGetValue(upperPlc, out int plv))
                throw new ArgumentException($"'{plc}' is not a valid Practice Location Character", nameof(plc));

            int[] weights = { 3, 5, 8, 4, 2, 1 };
            int sum = 0;
            for (int i = 0; i < 6; i++)
                sum += (digits6[i] - '0') * weights[i];
            sum += plv * 6;

            int remainder = sum % 11;
            if (remainder >= CheckTable.Length)
                throw new InvalidOperationException($"Remainder {remainder} out of check table range");
            return CheckTable[remainder];
        }

        // ─── Validation ──────────────────────────────────────────────────────────

        public static ValidationResult Validate(string input)
        {
            var result = new ValidationResult { InputValue = input };
            string clean = StripAlphaNumeric(input).ToUpper();
            string digits = new string(clean.Where(char.IsDigit).ToArray());

            // 16-digit HPI types
            if (digits.Length == 16 && digits.StartsWith(HpiiPrefix))
                return ValidateHpi(input, digits, HealthIdType.HPI_I, "HPI-I", HpiiPrefix);
            if (digits.Length == 16 && digits.StartsWith(HpioPrefix))
                return ValidateHpi(input, digits, HealthIdType.HPI_O, "HPI-O", HpioPrefix);
            if (digits.Length == 16 && digits.StartsWith(IhiPrefix))
                return ValidateHpi(input, digits, HealthIdType.IHI, "IHI", IhiPrefix);

            // Medicare card (10-11 digits, first digit 2-6)
            if (IsMedicareCandidate(digits))
                return ValidateMedicare(input, digits);

            // Provider Number: 8 chars — 6 digits + 1 alpha + 1 alpha
            if (IsProviderNumberCandidate(clean))
                return ValidateProviderNumber(input, clean);

            // Unknown
            result.IsValid = false;
            result.TypeLabel = "Unknown";
            result.FormatDescription = "Could not determine identifier type from input";
            result.Errors.Add("Input does not match any known Australian health identifier format.");
            result.Details.Add($"Cleaned input: {clean}");
            result.Details.Add("Expected formats:");
            result.Details.Add("  HPI-I/HPI-O/IHI — 16 digits starting with 800361 / 800362 / 800360");
            result.Details.Add("  Medicare card   — 10 digits, first digit 2–6  (e.g. 2123 45678 9 / 1)");
            result.Details.Add("  Provider Number — 6 digits + letter + check letter  (e.g. 234567A B)");
            return result;
        }

        private static ValidationResult ValidateHpi(string original, string clean, HealthIdType type,
            string label, string prefix)
        {
            var r = new ValidationResult
            {
                InputValue = original,
                DetectedType = type,
                TypeLabel = label,
                FormatDescription = $"16-digit {label} starting with {prefix}"
            };

            r.LengthStatus = clean.Length == 16
                ? "PASS — 16 digits"
                : $"FAIL — expected 16 digits, found {clean.Length}";
            if (!r.LengthStatus.StartsWith("PASS")) r.Errors.Add(r.LengthStatus);

            if (!IsAllDigits(clean))
                r.Errors.Add("Contains non-numeric characters after stripping formatting.");

            r.PrefixStatus = clean.StartsWith(prefix)
                ? $"PASS — prefix {prefix} correct"
                : $"FAIL — expected prefix {prefix}";
            if (!r.PrefixStatus.StartsWith("PASS")) r.Errors.Add(r.PrefixStatus);

            if (clean.Length == 16 && IsAllDigits(clean))
            {
                bool ok = LuhnValidate(clean);
                r.CheckDigitStatus = ok
                    ? $"PASS — Luhn check digit ({clean[^1]}) is correct"
                    : $"FAIL — Luhn check digit ({clean[^1]}) is incorrect";
                if (!ok) r.Errors.Add(r.CheckDigitStatus);
            }
            else r.CheckDigitStatus = "SKIP — cannot verify (length/format error)";

            r.Details.Add($"Full number:       {FormatHpi(clean)}");
            r.Details.Add($"Prefix (6 digits): {(clean.Length >= 6 ? clean[..6] : clean)}");
            r.Details.Add($"Body (9 digits):   {(clean.Length >= 15 ? clean[6..15] : "n/a")}");
            r.Details.Add($"Check digit:       {(clean.Length == 16 ? clean[15].ToString() : "n/a")}");

            r.IsValid = r.Errors.Count == 0;
            return r;
        }

        private static ValidationResult ValidateMedicare(string original, string clean)
        {
            var r = new ValidationResult
            {
                InputValue = original,
                DetectedType = HealthIdType.Medicare,
                TypeLabel = "Medicare",
                FormatDescription = "10-digit Medicare card number (optionally with /line suffix)"
            };

            string numberPart;
            string linePart = "";

            if (original.Contains('/'))
            {
                var parts = original.Replace(" ", "").Split('/');
                numberPart = new string(parts[0].Where(char.IsDigit).ToArray());
                linePart   = parts.Length > 1 ? parts[1].Trim() : "";
            }
            else if (clean.Length == 11)
            {
                numberPart = clean[..10];
                linePart   = clean[10].ToString();
            }
            else
            {
                numberPart = clean;
            }

            r.LengthStatus = numberPart.Length == 10
                ? "PASS — 10 digits"
                : $"FAIL — Medicare number must be 10 digits, found {numberPart.Length}";
            if (!r.LengthStatus.StartsWith("PASS")) r.Errors.Add(r.LengthStatus);

            if (!IsAllDigits(numberPart))
                r.Errors.Add("Medicare number contains non-numeric characters.");

            if (numberPart.Length >= 1)
            {
                int issuer = numberPart[0] - '0';
                r.PrefixStatus = Array.IndexOf(MedicareIssuers, issuer) >= 0
                    ? $"PASS — issuer digit {issuer} is valid ({IssuerState(issuer)})"
                    : $"FAIL — issuer digit {issuer} is not valid (must be 2–6)";
                if (!r.PrefixStatus.StartsWith("PASS")) r.Errors.Add(r.PrefixStatus);
            }

            if (numberPart.Length == 10 && IsAllDigits(numberPart))
            {
                // Check digit is at position 9 (index 8). Position 10 (index 9) is the issue number.
                int expected = MedicareLuhnCheck(numberPart[..8]);
                bool ok = (numberPart[8] - '0') == expected;
                r.CheckDigitStatus = ok
                    ? $"PASS — check digit ({numberPart[8]}) is correct"
                    : $"FAIL — check digit ({numberPart[8]}) is incorrect (expected {expected})";
                if (!ok) r.Errors.Add(r.CheckDigitStatus);
            }
            else r.CheckDigitStatus = "SKIP — cannot verify";

            r.Details.Add($"Card number:  {FormatMedicare(numberPart)}");
            r.Details.Add($"Issuer digit: {(numberPart.Length >= 1 ? numberPart[0].ToString() : "n/a")} ({(numberPart.Length >= 1 ? IssuerState(numberPart[0] - '0') : "")})");
            r.Details.Add($"Check digit:  {(numberPart.Length >= 9 ? numberPart[8].ToString() : "n/a")} (position 9)");
            r.Details.Add($"Issue number: {(numberPart.Length == 10 ? numberPart[9].ToString() : "n/a")} (position 10)");

            if (!string.IsNullOrEmpty(linePart))
            {
                bool lineOk = int.TryParse(linePart, out int lineVal) && lineVal >= 1 && lineVal <= 9;
                string lineStatus = lineOk
                    ? $"{linePart} — valid (1–9)"
                    : $"{linePart} — outside valid range (1–9)";
                r.Details.Add($"Line number:  {lineStatus}");
            }

            r.IsValid = r.Errors.Count == 0;
            return r;
        }

        private static ValidationResult ValidateProviderNumber(string original, string clean)
        {
            var r = new ValidationResult
            {
                InputValue = original,
                DetectedType = HealthIdType.ProviderNumber,
                TypeLabel = "Provider Number",
                FormatDescription = "Medicare Provider Number — 6-digit stem + PLC + check character"
            };

            string norm = clean.Replace(" ", "").ToUpper();

            r.LengthStatus = norm.Length == 8
                ? "PASS — 8 characters"
                : $"FAIL — expected 8 characters, found {norm.Length}";
            if (!r.LengthStatus.StartsWith("PASS")) { r.Errors.Add(r.LengthStatus); r.IsValid = false; return r; }

            string digits6  = norm[..6];
            char   plc      = norm[6];
            char   checkIn  = norm[7];

            // Digits check
            if (!IsAllDigits(digits6))
                r.Errors.Add($"First 6 characters must be digits, found: {digits6}");

            // PLC validity
            bool plcValid = PlvTable.ContainsKey(plc);
            if (!plcValid)
            {
                r.Errors.Add($"Position 7 (PLC) '{plc}' is not a valid Practice Location Character. Characters I, O and S are not permitted.");
                r.PrefixStatus = $"FAIL — PLC '{plc}' is invalid";
            }
            else
            {
                int plv = PlvTable[plc];
                r.PrefixStatus = $"PASS — PLC '{plc}' is valid (PLV={plv})";
            }

            // Check character
            if (IsAllDigits(digits6) && plcValid)
            {
                char expected;
                try { expected = ProviderNumberCheckChar(digits6, plc); }
                catch { expected = '?'; }

                bool ok = checkIn == expected;
                r.CheckDigitStatus = ok
                    ? $"PASS — check character '{checkIn}' is correct"
                    : $"FAIL — check character '{checkIn}' is incorrect (expected '{expected}')";
                if (!ok) r.Errors.Add(r.CheckDigitStatus);
            }
            else
            {
                r.CheckDigitStatus = "SKIP — cannot verify (format error in preceding fields)";
            }

            // State inference from numeric prefix
            string stateInfo = InferStateFromProviderDigits(digits6);
            r.Details.Add($"Full number:      {norm[..6]} {norm[6]} {norm[7]}");
            r.Details.Add($"Provider stem:    {digits6}");
            r.Details.Add($"PLC:              {plc}{(PlvTable.TryGetValue(plc, out int plvVal) ? $"  (PLV = {plvVal})" : " — invalid")}");
            r.Details.Add($"Check character:  {checkIn}");
            r.Details.Add($"Location / state: {stateInfo}");

            r.IsValid = r.Errors.Count == 0;
            return r;
        }

        // ─── Provider Number helpers ──────────────────────────────────────────────

        private static string InferStateFromProviderDigits(string digits6)
        {
            if (!IsAllDigits(digits6) || digits6.Length < 4) return "Unable to determine";
            int prefix4 = int.Parse(digits6[..4]);
            foreach (var (state, low, high) in StatePrefixes)
                if (prefix4 >= low && prefix4 <= high)
                    return $"Likely {state} (prefix {prefix4} in range {low}–{high})";
            return $"Prefix {prefix4} does not match a known state range";
        }

        // ─── Luhn helpers ────────────────────────────────────────────────────────

        public static int LuhnCheckDigit(string payload)
        {
            int sum = 0;
            bool doubleIt = true;
            for (int i = payload.Length - 1; i >= 0; i--)
            {
                int d = payload[i] - '0';
                if (doubleIt) { d *= 2; if (d > 9) d -= 9; }
                sum += d;
                doubleIt = !doubleIt;
            }
            return (10 - (sum % 10)) % 10;
        }

        public static bool LuhnValidate(string number)
        {
            int sum = 0;
            bool doubleIt = false;
            for (int i = number.Length - 1; i >= 0; i--)
            {
                int d = number[i] - '0';
                if (doubleIt) { d *= 2; if (d > 9) d -= 9; }
                sum += d;
                doubleIt = !doubleIt;
            }
            return sum % 10 == 0;
        }

        private static int MedicareLuhnCheck(string number)
        {
            // Per Services Australia spec: weights 1,3,7,9,1,3,7,9 applied to
            // digits 1–8 only. The check digit sits at position 9 (index 8).
            int[] weights = { 1, 3, 7, 9, 1, 3, 7, 9 };
            int sum = 0;
            for (int i = 0; i < 8 && i < number.Length; i++)
                sum += (number[i] - '0') * weights[i];
            return sum % 10;
        }

        // ─── Formatting helpers ──────────────────────────────────────────────────

        public static string FormatHpi(string raw)
        {
            if (raw.Length != 16) return raw;
            return $"{raw[..4]} {raw[4..8]} {raw[8..12]} {raw[12..16]}";
        }

        public static string FormatMedicare(string raw)
        {
            if (raw.Length < 10) return raw;
            return $"{raw[0]} {raw[1..4]} {raw[4..9]} {raw[9]}";
        }

        public static string FormatProviderNumber(string raw)
        {
            // Strip spaces first
            string n = raw.Replace(" ", "").ToUpper();
            if (n.Length != 8) return raw.ToUpper();
            // Format: NNNNNN L C  →  display as "NNNNNN LC" or spaced "NNN NNN LC"
            return $"{n[..3]} {n[3..6]} {n[6]}  {n[7]}";
        }

        public static string FormatForDisplay(HealthIdType type, string raw)
        {
            return type switch
            {
                HealthIdType.Medicare => FormatMedicare(
                    new string(raw.Where(char.IsDigit).ToArray()) is var d && d.Length >= 10
                        ? d[..10] : d)
                    + (raw.Contains('/') ? "/" + raw.Split('/')[1] : ""),
                HealthIdType.ProviderNumber => FormatProviderNumber(raw),
                _ => FormatHpi(new string(raw.Where(char.IsDigit).ToArray()))
            };
        }

        // ─── Utils ───────────────────────────────────────────────────────────────

        private static string StripAlphaNumeric(string input)
            => new string(input.Where(c => char.IsLetterOrDigit(c) || c == '/').ToArray());

        private static bool IsAllDigits(string s) => s.Length > 0 && s.All(char.IsDigit);

        private static bool IsMedicareCandidate(string digits)
        {
            if (digits.Length < 10 || digits.Length > 11) return false;
            int first = digits[0] - '0';
            return Array.IndexOf(MedicareIssuers, first) >= 0;
        }

        private static bool IsProviderNumberCandidate(string clean)
        {
            // 8 chars after stripping spaces: 6 digits, 1 alphanumeric PLC, 1 alpha check char
            string norm = clean.Replace(" ", "");
            if (norm.Length != 8) return false;
            return IsAllDigits(norm[..6]) && char.IsLetterOrDigit(norm[6]) && char.IsLetter(norm[7]);
        }

        private static string IssuerState(int digit) => digit switch
        {
            2 => "NSW/ACT",
            3 => "VIC/TAS",
            4 => "QLD",
            5 => "SA/NT",
            6 => "WA",
            _ => "Unknown"
        };

        public static string[] GetProviderStateOptions()
            => new[] { "Any State", "NSW/ACT", "VIC/TAS", "QLD", "SA/NT", "WA" };

        public static string GetTypeLabel(HealthIdType type) => type switch
        {
            HealthIdType.HPI_I          => "HPI-I",
            HealthIdType.HPI_O          => "HPI-O",
            HealthIdType.IHI            => "IHI",
            HealthIdType.Medicare       => "Medicare",
            HealthIdType.ProviderNumber => "Provider No.",
            _ => type.ToString()
        };

        public static string GetTypeDescription(HealthIdType type) => type switch
        {
            HealthIdType.HPI_I          => "Healthcare Provider Identifier – Individual",
            HealthIdType.HPI_O          => "Healthcare Provider Identifier – Organisation",
            HealthIdType.IHI            => "Individual Healthcare Identifier",
            HealthIdType.Medicare       => "Medicare Card Number",
            HealthIdType.ProviderNumber => "Medicare Provider Number",
            _ => ""
        };
    }
}
