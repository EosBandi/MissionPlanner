using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;
using log4net;
using MissionPlanner.ArduPilot.Mavlink;
using MissionPlanner.Controls;
using MissionPlanner.GCSViews;
using MissionPlanner.Utilities;
using CustomMessageBox = MissionPlanner.MsgBox.CustomMessageBox;

namespace MissionPlanner.plugins
{
    /// <summary>
    /// Motor test for scripted frames (FRAME_CLASS / Q_FRAME_CLASS 15, 16, 17).
    /// The motor layout of these frames is defined in a Lua script on the vehicle, so the
    /// standard Motor Test page cannot know the motors. This plugin downloads the scripts
    /// via MAVFTP (or loads a local .lua), finds the one that defines the matrix, extracts
    /// motor numbers / testing order and offers the usual DO_MOTOR_TEST buttons.
    /// </summary>
    public class ScriptingMotorTestPlugin : Plugin.Plugin
    {
        public override string Name => "Scripting Matrix Motor Test";
        public override string Version => "1.0";
        public override string Author => "Andras Schaffer";

        public override bool Init()
        {
            return true;
        }

        public override bool Loaded()
        {
            InitialSetup.AddPluginViewPage(typeof(ConfigScriptingMotorTest), "Scripting Motor Test",
                InitialSetup.pageOptions.isConnected | InitialSetup.pageOptions.gotAllParams);
            return true;
        }

        public override bool Exit()
        {
            return true;
        }
    }

    public class ScriptMotor
    {
        /// <summary>0-based motor index as used in Lua (0 = Motor1 output function), -1 if unknown</summary>
        public int MotorNum = -1;
        /// <summary>1-based testing order, used as DO_MOTOR_TEST param1</summary>
        public int TestOrder;
        public double? Roll;
        public double? Pitch;
        public double? Yaw;
        public double? ThrottleFactor;
        /// <summary>false for 6DoF, where the yaw factor is a thrust contribution and says nothing about prop direction</summary>
        public bool YawIsRotation = true;
        public string OutputLabel = "";

        public string Rotation
        {
            get
            {
                if (!YawIsRotation || !Yaw.HasValue || Yaw.Value == 0)
                    return "?";
                // AP_MOTORS_MATRIX_YAW_FACTOR_CW = -1, CCW = 1
                return Yaw.Value < 0 ? "CW" : "CCW";
            }
        }

        public string TestLetter
        {
            get { return LuaMatrixParser.TestLetter(TestOrder); }
        }
    }

    public class ScriptMatrix
    {
        public string FileName = "";
        public string Source = "";
        public int FrameClass;
        public int InitCount = -1;
        public List<ScriptMotor> Motors = new List<ScriptMotor>();
        public List<string> Warnings = new List<string>();

        public bool HasFactors
        {
            get { return FrameClass == 15 && Motors.Count > 0 && Motors.All(m => m.Roll.HasValue && m.Pitch.HasValue); }
        }

        public override string ToString()
        {
            return FileName;
        }
    }

    /// <summary>
    /// Regex based extraction of the motor definitions from a matrix Lua script. This is not a Lua
    /// interpreter: literal numbers, simple numeric constants and basic arithmetic are resolved,
    /// anything more dynamic falls back to the count given to init().
    /// </summary>
    public static class LuaMatrixParser
    {
        private class CallSpec
        {
            public string Obj;
            public string AddFunc;
            public int ArgCount;
            public int TestOrderArg;
            public int RollArg = -1;
            public int PitchArg = -1;
            public int YawArg = -1;
            public int ThrottleArg = -1;
        }

        private static CallSpec GetSpec(int frameClass)
        {
            switch (frameClass)
            {
                case 15:
                    // MotorsMatrix:add_motor_raw(motor_num, roll_factor, pitch_factor, yaw_factor, testing_order)
                    return new CallSpec
                    {
                        Obj = "MotorsMatrix", AddFunc = "add_motor_raw", ArgCount = 5, TestOrderArg = 4, RollArg = 1,
                        PitchArg = 2, YawArg = 3
                    };
                case 16:
                    // Motors_6DoF:add_motor(motor_num, roll, pitch, yaw, throttle, forward, right, reversible, testing_order)
                    return new CallSpec
                    {
                        Obj = "Motors_6DoF", AddFunc = "add_motor", ArgCount = 9, TestOrderArg = 8, RollArg = 1,
                        PitchArg = 2, YawArg = 3, ThrottleArg = 4
                    };
                case 17:
                    // Motors_dynamic:add_motor(motor_num, testing_order)
                    return new CallSpec { Obj = "Motors_dynamic", AddFunc = "add_motor", ArgCount = 2, TestOrderArg = 1 };
                default:
                    return null;
            }
        }

        public static bool IsScriptingFrame(int frameClass)
        {
            return frameClass == 15 || frameClass == 16 || frameClass == 17;
        }

        public static string TestLetter(int testOrder)
        {
            if (testOrder < 1)
                return "?";
            var s = "";
            var n = testOrder;
            while (n > 0)
            {
                n--;
                s = (char)('A' + n % 26) + s;
                n /= 26;
            }

            return s;
        }

        public static bool IsMatrixScript(string text, int frameClass)
        {
            var spec = GetSpec(frameClass);
            if (spec == null || text == null)
                return false;
            var code = StripComments(text);
            return Regex.IsMatch(code, @"\b" + spec.Obj + @"\s*:\s*init\s*\(");
        }

        /// <summary>
        /// Removes Lua comments while leaving string literals intact. String contents are blanked
        /// so they cannot produce false matches either.
        /// </summary>
        public static string StripComments(string text)
        {
            var sb = new StringBuilder(text.Length);
            int i = 0;
            while (i < text.Length)
            {
                char c = text[i];
                // comment
                if (c == '-' && i + 1 < text.Length && text[i + 1] == '-')
                {
                    int level;
                    if (TryLongBracket(text, i + 2, out level))
                    {
                        var close = "]" + new string('=', level) + "]";
                        int end = text.IndexOf(close, i + 2, StringComparison.Ordinal);
                        int stop = end < 0 ? text.Length : end + close.Length;
                        // keep newlines so line based checks still work
                        for (int k = i; k < stop; k++)
                            if (text[k] == '\n')
                                sb.Append('\n');
                        i = stop;
                    }
                    else
                    {
                        while (i < text.Length && text[i] != '\n')
                            i++;
                    }

                    continue;
                }

                // long string
                if (c == '[')
                {
                    int level;
                    if (TryLongBracket(text, i, out level))
                    {
                        var close = "]" + new string('=', level) + "]";
                        int end = text.IndexOf(close, i, StringComparison.Ordinal);
                        int stop = end < 0 ? text.Length : end + close.Length;
                        sb.Append("\"\"");
                        for (int k = i; k < stop; k++)
                            if (text[k] == '\n')
                                sb.Append('\n');
                        i = stop;
                        continue;
                    }
                }

                // quoted string
                if (c == '"' || c == '\'')
                {
                    char q = c;
                    i++;
                    while (i < text.Length && text[i] != q && text[i] != '\n')
                    {
                        if (text[i] == '\\')
                            i++;
                        i++;
                    }

                    i++;
                    sb.Append("\"\"");
                    continue;
                }

                sb.Append(c);
                i++;
            }

            return sb.ToString();
        }

        // checks for [[ or [==[ at pos
        private static bool TryLongBracket(string text, int pos, out int level)
        {
            level = 0;
            if (pos >= text.Length || text[pos] != '[')
                return false;
            int j = pos + 1;
            while (j < text.Length && text[j] == '=')
            {
                level++;
                j++;
            }

            return j < text.Length && text[j] == '[';
        }

        public static ScriptMatrix Parse(string fileName, string text, int frameClass)
        {
            var result = new ScriptMatrix { FileName = fileName, Source = text, FrameClass = frameClass };
            var spec = GetSpec(frameClass);
            if (spec == null)
            {
                result.Warnings.Add("Frame class " + frameClass + " is not a scripting frame class");
                return result;
            }

            var code = StripComments(text);
            var constants = CollectConstants(code);

            // init(N)
            var initMatch = Regex.Match(code, @"\b" + spec.Obj + @"\s*:\s*init\s*\(");
            if (initMatch.Success)
            {
                var args = ReadArgs(code, initMatch.Index + initMatch.Length);
                double n;
                if (args != null && args.Count >= 1 && TryEval(args[0], constants, out n))
                    result.InitCount = (int)Math.Round(n);
                else
                    result.Warnings.Add("Could not resolve the motor count passed to " + spec.Obj + ":init()");
            }
            else
            {
                result.Warnings.Add("No " + spec.Obj + ":init() call found");
            }

            var loops = FindLoopRanges(code);
            bool dynamicDefinition = false;

            foreach (Match m in Regex.Matches(code, @"\b" + spec.Obj + @"\s*:\s*" + spec.AddFunc + @"\s*\("))
            {
                var args = ReadArgs(code, m.Index + m.Length);
                int line = LineOf(code, m.Index);
                if (args == null || args.Count != spec.ArgCount)
                {
                    result.Warnings.Add(string.Format("Line {0}: {1}:{2}() has {3} arguments, expected {4}", line,
                        spec.Obj, spec.AddFunc, args == null ? 0 : args.Count, spec.ArgCount));
                    dynamicDefinition = true;
                    continue;
                }

                if (loops.Any(r => m.Index > r.Item1 && m.Index < r.Item2))
                {
                    result.Warnings.Add(string.Format("Line {0}: {1}:{2}() is called inside a loop", line, spec.Obj,
                        spec.AddFunc));
                    dynamicDefinition = true;
                    continue;
                }

                double motorNum, testOrder;
                if (!TryEval(args[0], constants, out motorNum) ||
                    !TryEval(args[spec.TestOrderArg], constants, out testOrder))
                {
                    result.Warnings.Add(string.Format(
                        "Line {0}: could not resolve motor number / testing order ({1}, {2})", line, args[0].Trim(),
                        args[spec.TestOrderArg].Trim()));
                    dynamicDefinition = true;
                    continue;
                }

                var motor = new ScriptMotor
                {
                    MotorNum = (int)Math.Round(motorNum),
                    TestOrder = (int)Math.Round(testOrder)
                };
                motor.Roll = EvalOrNull(args, spec.RollArg, constants);
                motor.Pitch = EvalOrNull(args, spec.PitchArg, constants);
                motor.Yaw = EvalOrNull(args, spec.YawArg, constants);
                motor.ThrottleFactor = EvalOrNull(args, spec.ThrottleArg, constants);
                motor.YawIsRotation = frameClass != 16;
                result.Motors.Add(motor);
            }

            if (dynamicDefinition || result.Motors.Count == 0)
            {
                if (result.InitCount > 0)
                {
                    result.Warnings.Add(string.Format(
                        "Motor layout could not be read statically, using {0} motors from init() with testing order 1..{0}",
                        result.InitCount));
                    result.Motors.Clear();
                    for (int a = 1; a <= result.InitCount; a++)
                        result.Motors.Add(new ScriptMotor { TestOrder = a });
                }
                else if (result.Motors.Count == 0)
                {
                    result.Warnings.Add("No motors found in script");
                }
            }

            Validate(result);

            result.Motors = result.Motors.OrderBy(a => a.TestOrder).ThenBy(a => a.MotorNum).ToList();
            return result;
        }

        private static void Validate(ScriptMatrix result)
        {
            if (result.InitCount > 0 && result.Motors.Count != result.InitCount)
                result.Warnings.Add(string.Format("init({0}) but {1} motors were defined", result.InitCount,
                    result.Motors.Count));

            foreach (var g in result.Motors.Where(a => a.MotorNum >= 0).GroupBy(a => a.MotorNum).Where(g => g.Count() > 1))
                result.Warnings.Add("Motor number " + (g.Key + 1) + " is defined more than once");

            foreach (var g in result.Motors.GroupBy(a => a.TestOrder).Where(g => g.Count() > 1))
                result.Warnings.Add("Testing order " + g.Key + " is used more than once");

            var orders = result.Motors.Select(a => a.TestOrder).Distinct().OrderBy(a => a).ToList();
            if (orders.Count > 0 && (orders[0] != 1 || orders[orders.Count - 1] != orders.Count))
                result.Warnings.Add("Testing order is not a contiguous 1.." + result.Motors.Count +
                                    " sequence, \"Test all in Sequence\" may skip motors");
        }

        private static double? EvalOrNull(List<string> args, int index, Dictionary<string, double> constants)
        {
            if (index < 0 || index >= args.Count)
                return null;
            double v;
            if (TryEval(args[index], constants, out v))
                return v;
            return null;
        }

        private static int LineOf(string code, int index)
        {
            int line = 1;
            for (int i = 0; i < index && i < code.Length; i++)
                if (code[i] == '\n')
                    line++;
            return line;
        }

        /// <summary>
        /// Approximate ranges of "for ... do ... end" / "while ... do ... end" bodies.
        /// </summary>
        private static List<Tuple<int, int>> FindLoopRanges(string code)
        {
            var ranges = new List<Tuple<int, int>>();
            var tokens = Regex.Matches(code, @"\b(for|while|repeat|function|if|do|end|until)\b");
            // stack of (keyword, startIndex)
            var stack = new Stack<Tuple<string, int>>();
            string pendingLoop = null;
            int pendingStart = 0;
            foreach (Match t in tokens)
            {
                var w = t.Value;
                switch (w)
                {
                    case "for":
                    case "while":
                        pendingLoop = w;
                        pendingStart = t.Index;
                        break;
                    case "do":
                        if (pendingLoop != null)
                        {
                            stack.Push(Tuple.Create("loop", pendingStart));
                            pendingLoop = null;
                        }
                        else
                        {
                            stack.Push(Tuple.Create("block", t.Index));
                        }

                        break;
                    case "function":
                    case "if":
                        stack.Push(Tuple.Create("block", t.Index));
                        break;
                    case "repeat":
                        stack.Push(Tuple.Create("repeat", t.Index));
                        break;
                    case "until":
                        if (stack.Count > 0)
                        {
                            var r = stack.Pop();
                            ranges.Add(Tuple.Create(r.Item2, t.Index));
                        }

                        break;
                    case "end":
                        if (stack.Count > 0)
                        {
                            var b = stack.Pop();
                            if (b.Item1 == "loop")
                                ranges.Add(Tuple.Create(b.Item2, t.Index));
                        }

                        break;
                }
            }

            return ranges;
        }

        /// <summary>
        /// Reads a comma separated argument list starting just after the opening parenthesis.
        /// </summary>
        private static List<string> ReadArgs(string code, int start)
        {
            var args = new List<string>();
            int depth = 0;
            var cur = new StringBuilder();
            for (int i = start; i < code.Length; i++)
            {
                char c = code[i];
                if (c == '(' || c == '{' || c == '[')
                {
                    depth++;
                }
                else if (c == ')' || c == '}' || c == ']')
                {
                    if (depth == 0)
                    {
                        if (cur.ToString().Trim().Length > 0 || args.Count > 0)
                            args.Add(cur.ToString());
                        return args;
                    }

                    depth--;
                }
                else if (c == ',' && depth == 0)
                {
                    args.Add(cur.ToString());
                    cur.Clear();
                    continue;
                }

                cur.Append(c);
            }

            return null;
        }

        private static Dictionary<string, double> CollectConstants(string code)
        {
            var constants = new Dictionary<string, double>();
            // a few passes so constants defined from other constants resolve
            var assigns = Regex.Matches(code,
                @"^[ \t]*(?:local[ \t]+)?([A-Za-z_][A-Za-z0-9_]*)[ \t]*=[ \t]*([^\r\n;]+)", RegexOptions.Multiline);
            var multiAssigned = new HashSet<string>(assigns.Cast<Match>().GroupBy(m => m.Groups[1].Value)
                .Where(g => g.Count() > 1).Select(g => g.Key));
            for (int pass = 0; pass < 3; pass++)
            {
                foreach (Match m in assigns)
                {
                    var name = m.Groups[1].Value;
                    // values that change during the script are not constants
                    if (multiAssigned.Contains(name) || constants.ContainsKey(name))
                        continue;
                    double v;
                    if (TryEval(m.Groups[2].Value, constants, out v))
                        constants[name] = v;
                }
            }

            return constants;
        }

        public static bool TryEval(string expr, Dictionary<string, double> constants, out double value)
        {
            value = 0;
            if (expr == null)
                return false;
            try
            {
                var p = new ExprParser(expr, constants);
                value = p.ParseExpression();
                p.SkipWs();
                if (!p.AtEnd)
                    return false;
                return !double.IsNaN(value) && !double.IsInfinity(value);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Recursive descent evaluator for numeric Lua expressions: + - * / % ^, parentheses,
        /// unary minus, known constants, math.pi and math.sin/cos/rad/deg/sqrt/abs/floor/ceil.
        /// </summary>
        private class ExprParser
        {
            private readonly string s;
            private int pos;
            private readonly Dictionary<string, double> constants;

            public ExprParser(string s, Dictionary<string, double> constants)
            {
                this.s = s;
                this.constants = constants;
            }

            public bool AtEnd
            {
                get { return pos >= s.Length; }
            }

            public void SkipWs()
            {
                while (pos < s.Length && char.IsWhiteSpace(s[pos]))
                    pos++;
            }

            private bool Accept(char c)
            {
                SkipWs();
                if (pos < s.Length && s[pos] == c)
                {
                    pos++;
                    return true;
                }

                return false;
            }

            public double ParseExpression()
            {
                double v = ParseTerm();
                while (true)
                {
                    if (Accept('+'))
                        v += ParseTerm();
                    else if (Accept('-'))
                        v -= ParseTerm();
                    else
                        return v;
                }
            }

            private double ParseTerm()
            {
                double v = ParseUnary();
                while (true)
                {
                    SkipWs();
                    if (pos + 1 < s.Length && s[pos] == '/' && s[pos + 1] == '/')
                    {
                        pos += 2;
                        v = Math.Floor(v / ParseUnary());
                    }
                    else if (Accept('*'))
                        v *= ParseUnary();
                    else if (Accept('/'))
                        v /= ParseUnary();
                    else if (Accept('%'))
                    {
                        var d = ParseUnary();
                        v = v - Math.Floor(v / d) * d;
                    }
                    else
                        return v;
                }
            }

            private double ParseUnary()
            {
                if (Accept('-'))
                    return -ParseUnary();
                if (Accept('+'))
                    return ParseUnary();
                return ParsePower();
            }

            private double ParsePower()
            {
                double b = ParsePrimary();
                if (Accept('^'))
                    return Math.Pow(b, ParseUnary());
                return b;
            }

            private double ParsePrimary()
            {
                SkipWs();
                if (Accept('('))
                {
                    var v = ParseExpression();
                    if (!Accept(')'))
                        throw new FormatException("missing )");
                    return v;
                }

                if (pos < s.Length && (char.IsDigit(s[pos]) || s[pos] == '.'))
                {
                    if (pos + 1 < s.Length && s[pos] == '0' && (s[pos + 1] == 'x' || s[pos + 1] == 'X'))
                    {
                        int hs = pos + 2;
                        pos = hs;
                        while (pos < s.Length && Uri.IsHexDigit(s[pos]))
                            pos++;
                        return Convert.ToInt64(s.Substring(hs, pos - hs), 16);
                    }

                    int start = pos;
                    while (pos < s.Length && (char.IsDigit(s[pos]) || s[pos] == '.'))
                        pos++;
                    if (pos < s.Length && (s[pos] == 'e' || s[pos] == 'E'))
                    {
                        pos++;
                        if (pos < s.Length && (s[pos] == '+' || s[pos] == '-'))
                            pos++;
                        while (pos < s.Length && char.IsDigit(s[pos]))
                            pos++;
                    }

                    return double.Parse(s.Substring(start, pos - start), NumberStyles.Float,
                        CultureInfo.InvariantCulture);
                }

                if (pos < s.Length && (char.IsLetter(s[pos]) || s[pos] == '_'))
                {
                    int start = pos;
                    while (pos < s.Length && (char.IsLetterOrDigit(s[pos]) || s[pos] == '_' || s[pos] == '.'))
                        pos++;
                    var name = s.Substring(start, pos - start);

                    if (name == "math.pi")
                        return Math.PI;
                    if (name == "math.huge")
                        throw new FormatException("infinite");

                    SkipWs();
                    if (pos < s.Length && s[pos] == '(')
                    {
                        pos++;
                        var arg = ParseExpression();
                        if (!Accept(')'))
                            throw new FormatException("missing )");
                        switch (name)
                        {
                            case "math.sin": return Math.Sin(arg);
                            case "math.cos": return Math.Cos(arg);
                            case "math.tan": return Math.Tan(arg);
                            case "math.rad": return arg * Math.PI / 180.0;
                            case "math.deg": return arg * 180.0 / Math.PI;
                            case "math.sqrt": return Math.Sqrt(arg);
                            case "math.abs": return Math.Abs(arg);
                            case "math.floor": return Math.Floor(arg);
                            case "math.ceil": return Math.Ceiling(arg);
                            case "tonumber": return arg;
                            default: throw new FormatException("unknown function " + name);
                        }
                    }

                    double v;
                    if (constants != null && constants.TryGetValue(name, out v))
                        return v;
                    throw new FormatException("unknown identifier " + name);
                }

                throw new FormatException("unexpected input");
            }
        }
    }

    public class ConfigScriptingMotorTest : MyUserControl, IActivate
    {
        private static readonly ILog log =
            LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        private static readonly string[] ScriptDirs = { "/APM/scripts", "APM/scripts", "/scripts", "@ROMFS/scripts" };
        private const ulong MaxScriptSize = 256 * 1024;

        // parsed scripts per sysid, so revisiting the page does not download again
        private static readonly Dictionary<uint, List<ScriptMatrix>> cache = new Dictionary<uint, List<ScriptMatrix>>();
        private static bool safetyConfirmed;

        private readonly Label lblFrame;
        private readonly Label lblStatus;
        private readonly ComboBox cmbScript;
        private readonly MyButton butReread;
        private readonly MyButton butLoadLocal;
        private readonly TextBox txtWarnings;
        private readonly NumericUpDown numThrottle;
        private readonly NumericUpDown numDuration;
        private readonly Panel panelMotors;
        private readonly Panel panelDiagram;
        private readonly FlowLayoutPanel panelAll;

        private int frameClass = -1;
        private ScriptMatrix current;

        public ConfigScriptingMotorTest()
        {
            Dock = DockStyle.Fill;
            AutoScroll = true;
            MinimumSize = new Size(700, 500);

            int y = 6;
            lblFrame = new Label { Location = new Point(6, y), AutoSize = true, Font = new Font(Font, FontStyle.Bold) };
            Controls.Add(lblFrame);
            y += 22;

            lblStatus = new Label { Location = new Point(6, y), AutoSize = true };
            Controls.Add(lblStatus);
            y += 22;

            Controls.Add(new Label { Text = "Script:", Location = new Point(6, y + 4), AutoSize = true });
            cmbScript = new ComboBox
            {
                Location = new Point(60, y), Width = 260, DropDownStyle = ComboBoxStyle.DropDownList
            };
            cmbScript.SelectedIndexChanged += (s, e) => ShowMatrix(cmbScript.SelectedItem as ScriptMatrix);
            Controls.Add(cmbScript);

            butReread = new MyButton { Text = "Re-read from vehicle", Location = new Point(330, y - 1), Size = new Size(130, 25) };
            butReread.Click += (s, e) =>
            {
                cache.Remove(MainV2.comPort.MAV.sysid);
                LoadFromVehicle();
            };
            Controls.Add(butReread);

            butLoadLocal = new MyButton { Text = "Load local .lua...", Location = new Point(466, y - 1), Size = new Size(120, 25) };
            butLoadLocal.Click += but_LoadLocal_Click;
            Controls.Add(butLoadLocal);
            y += 30;

            txtWarnings = new TextBox
            {
                Location = new Point(6, y), Size = new Size(680, 60), Multiline = true, ReadOnly = true,
                ScrollBars = ScrollBars.Vertical
            };
            Controls.Add(txtWarnings);
            y += 68;

            var lblSafety = new Label
            {
                Text = "NOTE: REMOVE PROPELLERS OR HOLD DOWN YOUR VEHICLE. Buttons follow the testing order defined in the script.",
                Location = new Point(6, y), AutoSize = true, ForeColor = Color.Red
            };
            Controls.Add(lblSafety);
            y += 22;

            Controls.Add(new Label { Text = "Throttle %", Location = new Point(6, y + 3), AutoSize = true });
            numThrottle = new NumericUpDown { Location = new Point(80, y), Width = 60, Minimum = -100, Maximum = 100, Value = 5 };
            Controls.Add(numThrottle);
            Controls.Add(new Label { Text = "Duration (s)", Location = new Point(160, y + 3), AutoSize = true });
            numDuration = new NumericUpDown { Location = new Point(240, y), Width = 60, Minimum = 0, Maximum = 999, Value = 2 };
            Controls.Add(numDuration);

            var link = new LinkLabel
            {
                Text = "Motor test documentation", Location = new Point(330, y + 3), AutoSize = true
            };
            link.LinkClicked += (s, e) =>
            {
                try
                {
                    Process.Start("https://ardupilot.org/copter/docs/connect-escs-and-motors.html#testing-motor-spin-directions");
                }
                catch
                {
                    CustomMessageBox.Show("Bad default system association", Strings.ERROR);
                }
            };
            Controls.Add(link);
            y += 32;

            panelMotors = new Panel { Location = new Point(6, y), Size = new Size(440, 100), AutoSize = true };
            Controls.Add(panelMotors);

            panelDiagram = new Panel { Location = new Point(456, y), Size = new Size(230, 230), BorderStyle = BorderStyle.FixedSingle };
            panelDiagram.Paint += panelDiagram_Paint;
            Controls.Add(panelDiagram);

            panelAll = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, WrapContents = false };
            AddAllButton("Test all motors", but_TestAll_Click);
            AddAllButton("Stop all motors", but_StopAll_Click);
            AddAllButton("Test all in Sequence", but_TestAllSeq_Click);
        }

        private void AddAllButton(string text, EventHandler handler)
        {
            var b = new MyButton { Text = text, Size = new Size(85, 37) };
            b.Click += handler;
            panelAll.Controls.Add(b);
        }

        public void Activate()
        {
            current = null;
            cmbScript.Items.Clear();
            panelMotors.Controls.Clear();
            panelDiagram.Visible = false;
            txtWarnings.Text = "";

            frameClass = -1;
            string paramName = null;
            foreach (var name in new[] { "FRAME_CLASS", "Q_FRAME_CLASS" })
            {
                if (MainV2.comPort.MAV.param.ContainsKey(name) && MainV2.comPort.MAV.param[name] != null)
                {
                    paramName = name;
                    frameClass = (int)MainV2.comPort.MAV.param[name].Value;
                    break;
                }
            }

            if (paramName == null)
            {
                lblFrame.Text = "No FRAME_CLASS or Q_FRAME_CLASS parameter found";
                SetEnabled(false);
                return;
            }

            lblFrame.Text = paramName + " = " + frameClass + " (" + FrameClassName(paramName, frameClass) + ")";

            if (!LuaMatrixParser.IsScriptingFrame(frameClass))
            {
                lblStatus.Text = "This is not a scripting frame (15, 16 or 17) - use the standard Motor Test page.";
                SetEnabled(false);
                return;
            }

            SetEnabled(true);

            if (!MainV2.comPort.MAV.param.ContainsKey("SCR_ENABLE") ||
                MainV2.comPort.MAV.param["SCR_ENABLE"].Value == 0)
                lblStatus.Text = "WARNING: SCR_ENABLE is not set, the matrix script is not running and motor tests will likely be denied.";
            else
                lblStatus.Text = "";

            List<ScriptMatrix> cached;
            if (cache.TryGetValue(MainV2.comPort.MAV.sysid, out cached) && cached.Count > 0 &&
                cached[0].FrameClass == frameClass)
                ShowScripts(cached);
            else
                LoadFromVehicle();
        }

        private void SetEnabled(bool enabled)
        {
            cmbScript.Enabled = enabled;
            butReread.Enabled = enabled;
            butLoadLocal.Enabled = enabled;
            numThrottle.Enabled = enabled;
            numDuration.Enabled = enabled;
            panelMotors.Enabled = enabled;
        }

        private static string FrameClassName(string paramName, int value)
        {
            try
            {
                var options = ParameterMetaDataRepository.GetParameterOptionsInt(paramName,
                    MainV2.comPort.MAV.cs.firmware.ToString());
                foreach (var kv in options)
                    if (kv.Key == value)
                        return kv.Value.Trim();
            }
            catch (Exception ex)
            {
                log.Debug(ex);
            }

            return "unknown";
        }

        private void LoadFromVehicle()
        {
            if ((MainV2.comPort.MAV.cs.capabilities & (int)MAVLink.MAV_PROTOCOL_CAPABILITY.FTP) == 0)
            {
                txtWarnings.Text = "The autopilot does not support MAVFTP. Use \"Load local .lua...\" to select the matrix script.";
                return;
            }

            var found = new List<ScriptMatrix>();
            var errors = new List<string>();
            int fc = frameClass;

            var mavftp = new MAVFtp(MainV2.comPort, MainV2.comPort.MAV.sysid, MainV2.comPort.MAV.compid);
            var prd = new ProgressReporterDialogue { Text = "Reading scripts from vehicle" };
            var cancel = new CancellationTokenSource();

            prd.doWorkArgs.CancelRequestChanged += (o, args) =>
            {
                prd.doWorkArgs.ErrorMessage = "User Cancel";
                cancel.Cancel();
                mavftp.kCmdResetSessions();
            };
            prd.doWorkArgs.ForceExit = false;

            prd.DoWork += (iprd) =>
            {
                var seen = new HashSet<string>();
                foreach (var dir in ScriptDirs)
                {
                    if (cancel.IsCancellationRequested)
                        break;
                    iprd.UpdateProgressAndStatus(-1, "Listing " + dir);
                    List<MAVFtp.FtpFileInfo> list;
                    try
                    {
                        list = mavftp.kCmdListDirectory(dir, cancel);
                    }
                    catch (Exception ex)
                    {
                        log.Info("list " + dir + " failed: " + ex.Message);
                        continue;
                    }

                    if (list == null)
                        continue;

                    var luaFiles = list.Where(f => !f.isDirectory && !string.IsNullOrEmpty(f.Name) &&
                                                   f.Name.EndsWith(".lua", StringComparison.OrdinalIgnoreCase))
                        .ToList();
                    log.Info(dir + ": " + luaFiles.Count + " lua files");

                    foreach (var file in luaFiles)
                    {
                        if (cancel.IsCancellationRequested)
                            break;
                        // the same SD card can show up under several of the candidate paths
                        var key = file.Name + ":" + file.Size;
                        if (dir.TrimStart('/') == "APM/scripts" && seen.Contains(key))
                            continue;
                        seen.Add(key);

                        if (file.Size > MaxScriptSize)
                        {
                            errors.Add(file.FullName + " skipped, too large (" + file.Size + " bytes)");
                            continue;
                        }

                        iprd.UpdateProgressAndStatus(-1, "Downloading " + file.FullName);
                        try
                        {
                            var ms = mavftp.GetFile(file.FullName, cancel, false);
                            if (ms == null || ms.Length == 0)
                            {
                                errors.Add(file.FullName + " could not be downloaded");
                                continue;
                            }

                            var text = Encoding.UTF8.GetString(ms.ToArray());
                            if (LuaMatrixParser.IsMatrixScript(text, fc))
                                found.Add(LuaMatrixParser.Parse(file.FullName, text, fc));
                        }
                        catch (Exception ex)
                        {
                            errors.Add(file.FullName + ": " + ex.Message);
                        }
                    }
                }

                if (cancel.IsCancellationRequested)
                {
                    iprd.doWorkArgs.CancelAcknowledged = true;
                    iprd.doWorkArgs.CancelRequested = true;
                }
            };

            prd.RunBackgroundOperationAsync();

            if (found.Count > 0)
                cache[MainV2.comPort.MAV.sysid] = found;

            ShowScripts(found);

            if (found.Count == 0)
            {
                errors.Insert(0, "No matrix script (" + ObjName(fc) +
                                 ":init) found on the vehicle. Use \"Load local .lua...\" to select it manually.");
            }

            if (errors.Count > 0)
                txtWarnings.Text = string.Join(Environment.NewLine, errors) +
                                   (txtWarnings.Text.Length > 0 ? Environment.NewLine + txtWarnings.Text : "");
        }

        private static string ObjName(int fc)
        {
            switch (fc)
            {
                case 16: return "Motors_6DoF";
                case 17: return "Motors_dynamic";
                default: return "MotorsMatrix";
            }
        }

        private void but_LoadLocal_Click(object sender, EventArgs e)
        {
            using (var ofd = new OpenFileDialog { Filter = "Lua scripts|*.lua|All files|*.*" })
            {
                if (ofd.ShowDialog() != DialogResult.OK)
                    return;
                try
                {
                    var text = File.ReadAllText(ofd.FileName);
                    if (!LuaMatrixParser.IsMatrixScript(text, frameClass))
                    {
                        if (CustomMessageBox.Show(
                                "The selected file does not call " + ObjName(frameClass) +
                                ":init(). Parse it anyway?", "Scripting Motor Test", MessageBoxButtons.YesNo) !=
                            DialogResult.Yes)
                            return;
                    }

                    var matrix = LuaMatrixParser.Parse(Path.GetFileName(ofd.FileName) + " (local)", text, frameClass);
                    var list = cmbScript.Items.Cast<ScriptMatrix>().ToList();
                    list.Add(matrix);
                    cache[MainV2.comPort.MAV.sysid] = list;
                    ShowScripts(list);
                    cmbScript.SelectedItem = matrix;
                }
                catch (Exception ex)
                {
                    CustomMessageBox.Show("Failed to read script\n" + ex.Message, Strings.ERROR);
                }
            }
        }

        private void ShowScripts(List<ScriptMatrix> scripts)
        {
            cmbScript.Items.Clear();
            foreach (var s in scripts)
                cmbScript.Items.Add(s);

            if (scripts.Count > 0)
                cmbScript.SelectedIndex = 0;
            else
                ShowMatrix(null);
        }

        private void ShowMatrix(ScriptMatrix matrix)
        {
            current = matrix;
            panelMotors.Controls.Clear();

            if (matrix == null)
            {
                panelDiagram.Visible = false;
                txtWarnings.Text = "";
                return;
            }

            AssignOutputs(matrix);

            var warnings = new List<string>(matrix.Warnings);
            if (cmbScript.Items.Count > 1)
                warnings.Insert(0, cmbScript.Items.Count + " matrix scripts found, select the one in use.");
            warnings.AddRange(matrix.Motors.Where(m => m.MotorNum >= 0 && m.OutputLabel == "")
                .Select(m => "Motor " + (m.MotorNum + 1) + " is not assigned to any output (SERVOx_FUNCTION = " +
                             (33 + m.MotorNum) + ")"));
            txtWarnings.Text = warnings.Count == 0 ? "Script parsed without warnings." : string.Join(Environment.NewLine, warnings);

            int y = 0;
            foreach (var motor in matrix.Motors)
            {
                var but = new MyButton
                {
                    Text = "Test motor " + motor.TestLetter, Location = new Point(0, y), Size = new Size(85, 22),
                    Tag = motor.TestOrder
                };
                but.Click += but_TestMotor_Click;
                panelMotors.Controls.Add(but);

                var lbl = new Label
                {
                    Text = MotorDescription(motor), Location = new Point(92, y + 4), AutoSize = true
                };
                panelMotors.Controls.Add(lbl);
                y += 25;
            }

            panelAll.Location = new Point(0, y + 5);
            panelMotors.Controls.Add(panelAll);

            panelDiagram.Visible = matrix.HasFactors;
            panelDiagram.Invalidate();

            ThemeManager.ApplyThemeTo(this);
        }

        private static string MotorDescription(ScriptMotor m)
        {
            var parts = new List<string>();
            parts.Add(m.MotorNum >= 0 ? "Motor " + (m.MotorNum + 1) : "Motor ?");
            if (m.Rotation != "?")
                parts.Add(m.Rotation);
            if (m.Roll.HasValue && m.Pitch.HasValue && m.Yaw.HasValue)
                parts.Add(string.Format(CultureInfo.InvariantCulture, "R {0:0.###} P {1:0.###} Y {2:0.###}", m.Roll,
                    m.Pitch, m.Yaw));
            if (m.ThrottleFactor.HasValue)
                parts.Add(string.Format(CultureInfo.InvariantCulture, "T {0:0.###}", m.ThrottleFactor));
            if (m.OutputLabel != "")
                parts.Add(m.OutputLabel);
            return string.Join("  ·  ", parts);
        }

        private static void AssignOutputs(ScriptMatrix matrix)
        {
            foreach (var m in matrix.Motors)
            {
                m.OutputLabel = "";
                if (m.MotorNum < 0)
                    continue;
                var outputs = new List<string>();
                for (int i = 1; i <= 32; i++)
                {
                    var name = "SERVO" + i + "_FUNCTION";
                    if (MainV2.comPort.MAV.param.ContainsKey(name) && MainV2.comPort.MAV.param[name] != null &&
                        (int)MainV2.comPort.MAV.param[name].Value == 33 + m.MotorNum)
                        outputs.Add("SERVO" + i);
                }

                m.OutputLabel = string.Join(",", outputs);
            }
        }

        private void panelDiagram_Paint(object sender, PaintEventArgs e)
        {
            if (current == null || !current.HasFactors)
                return;

            var g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            var size = panelDiagram.ClientSize;
            float cx = size.Width / 2f, cy = size.Height / 2f;
            float r = 16;
            float max = (float)current.Motors.Max(m => Math.Max(Math.Abs(m.Roll.Value), Math.Abs(m.Pitch.Value)));
            if (max <= 0)
                max = 1;
            float scale = (Math.Min(size.Width, size.Height) / 2f - r - 4) / max;

            using (var fg = new SolidBrush(panelDiagram.ForeColor))
            using (var pen = new Pen(panelDiagram.ForeColor))
            using (var font = new Font(Font.FontFamily, 7.5f))
            {
                // front marker
                g.DrawString("FRONT", font, fg, cx - 16, 2);
                g.DrawLine(pen, cx, 14, cx, cy - 8);
                g.DrawEllipse(pen, cx - 4, cy - 4, 8, 8);

                foreach (var m in current.Motors)
                {
                    // a motor on the right side gives negative roll factor, front motor positive pitch factor
                    float x = cx - (float)m.Roll.Value * scale;
                    float y = cy - (float)m.Pitch.Value * scale;
                    var color = m.Rotation == "CW" ? Color.SteelBlue : m.Rotation == "CCW" ? Color.SeaGreen : Color.Gray;
                    using (var b = new SolidBrush(color))
                        g.FillEllipse(b, x - r, y - r, 2 * r, 2 * r);
                    var text = m.TestLetter + "\n" + (m.MotorNum >= 0 ? (m.MotorNum + 1).ToString() : "?");
                    var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                    g.DrawString(text, font, Brushes.White, new RectangleF(x - r, y - r, 2 * r, 2 * r), sf);
                }

                g.DrawString("letter = test order, number = motor", font, fg, 2, size.Height - 14);
            }
        }

        private bool CheckSafety()
        {
            if (MainV2.comPort.MAV.cs.armed)
            {
                CustomMessageBox.Show("The vehicle is armed. Disarm before running a motor test.", Strings.ERROR);
                return false;
            }

            if (safetyConfirmed)
                return true;

            if (CustomMessageBox.Show("Motors will spin. Make sure the propellers are removed or the vehicle is held down.\n\nContinue?",
                    "Scripting Motor Test", MessageBoxButtons.YesNo) != DialogResult.Yes)
                return false;

            safetyConfirmed = true;
            return true;
        }

        private void but_TestMotor_Click(object sender, EventArgs e)
        {
            if (!CheckSafety())
                return;
            testMotor((int)((Control)sender).Tag, (int)numThrottle.Value, (int)numDuration.Value);
        }

        private void but_TestAll_Click(object sender, EventArgs e)
        {
            if (current == null || !CheckSafety())
                return;
            foreach (var order in current.Motors.Select(m => m.TestOrder).Distinct())
                testMotor(order, (int)numThrottle.Value, (int)numDuration.Value);
        }

        private void but_StopAll_Click(object sender, EventArgs e)
        {
            if (current == null)
                return;
            foreach (var order in current.Motors.Select(m => m.TestOrder).Distinct())
                testMotor(order, 0, 0);
        }

        private void but_TestAllSeq_Click(object sender, EventArgs e)
        {
            if (current == null || !CheckSafety())
                return;
            testMotor(1, (int)numThrottle.Value, (int)numDuration.Value,
                current.Motors.Select(m => m.TestOrder).Distinct().Count());
        }

        // same command as the standard Motor Test page; param1 is the testing order, not the motor number
        private void testMotor(int testOrder, int speed, int time, int motorcount = 0)
        {
            try
            {
                if (!MainV2.comPort.doCommand((byte)MainV2.comPort.sysidcurrent,
                        (byte)MainV2.comPort.compidcurrent,
                        MAVLink.MAV_CMD.DO_MOTOR_TEST,
                        (float)testOrder,
                        (float)(byte)MAVLink.MOTOR_TEST_THROTTLE_TYPE.MOTOR_TEST_THROTTLE_PERCENT,
                        (float)speed,
                        (float)time,
                        (float)motorcount,
                        0,
                        0))
                {
                    CustomMessageBox.Show("Command was denied by the autopilot");
                }
            }
            catch (Exception ex)
            {
                log.Error(ex);
                CustomMessageBox.Show(Strings.ErrorCommunicating + "\nMotor: " + LuaMatrixParser.TestLetter(testOrder),
                    Strings.ERROR);
            }
        }
    }
}
