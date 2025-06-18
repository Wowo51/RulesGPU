using RulesDMN;
using RulesDMN.Models;
using RulesGPU;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using TorchSharp;
using static TorchSharp.torch;

namespace RulesGPUApp
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly Device _device;

        public MainWindow()
        {
            InitializeComponent();

            // pick CUDA if present
            _device = cuda.is_available() ? new Device(DeviceType.CUDA) : new Device(DeviceType.CPU);

            RulesText.Text = DefaultDmn;
            RecordsText.Text = DefaultCsv;
        }

        private void SolveButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // --- parse DMN --------------------------------------------------
                string dmnXml = RulesText.Text;
                DmnModel? model = DmnParser.ParseDmn(dmnXml);

                if (model is null || model.Decisions.Count == 0 ||
                    model.Decisions[0].DecisionLogic is not DecisionTable dt)
                {
                    OutputText.Text = "❌  Could not parse DMN.";
                    return;
                }

                using var gpuTable = DmnToGpuConverter.ConvertDecisionTableToGpuRepresentation(dt, _device);
                var engine = new RulesGPUEngine(_device);

                // --- parse records ---------------------------------------------
                List<IReadOnlyDictionary<string, object>> records =
                    CsvRecordParser.Parse(RecordsText.Text);

                // --- evaluate ---------------------------------------------------
                var results = engine.Evaluate(gpuTable, records);

                // --- dump as JSON ----------------------------------------------
                var sb = new StringBuilder();
                var opts = new JsonSerializerOptions { WriteIndented = true };
                for (int i = 0; i < results.Count; i++)
                {
                    sb.AppendLine($"# Record {i + 1}");
                    sb.AppendLine(JsonSerializer.Serialize(results[i], opts));
                    sb.AppendLine();
                }

                OutputText.Text = sb.ToString();
            }
            catch (Exception ex)
            {
                OutputText.Text = $"❌ Error:\n{ex}";
            }
        }

        // --------------------------------------------------------------------
        // Default sample data
        // --------------------------------------------------------------------
        private const string DefaultDmn =
@"<?xml version=""1.0"" encoding=""UTF-8""?>
<definitions id=""loanEligibility"" name=""LoanEligibilityDecision""
             xmlns=""https://www.omg.org/spec/DMN/20180521/MODEL/"">
  <decision id=""LoanDecision"" name=""Loan Decision"">
    <decisionTable id=""loanDecisionTable"" hitPolicy=""FIRST"">
      <input id=""ageIn"" name=""Applicant Age"">
        <inputExpression id=""ageExpr"" typeRef=""number"">
          <text>Applicant Age</text>
        </inputExpression>
      </input>
      <input id=""incIn"" name=""Applicant Income"">
        <inputExpression id=""incExpr"" typeRef=""number"">
          <text>Applicant Income</text>
        </inputExpression>
      </input>
      <input id=""credIn"" name=""Credit Score"">
        <inputExpression id=""credExpr"" typeRef=""number"">
          <text>Credit Score</text>
        </inputExpression>
      </input>

      <output id=""decisionOut"" name=""Decision"" typeRef=""string""/>

      <!-- Rules -->
      <rule><inputEntry><text>&lt; 18</text></inputEntry><inputEntry><text>-</text></inputEntry><inputEntry><text>-</text></inputEntry><outputEntry><text>""Declined (Underage)""</text></outputEntry></rule>
      <rule><inputEntry><text>&gt;= 18</text></inputEntry><inputEntry><text>-</text></inputEntry><inputEntry><text>&lt; 600</text></inputEntry><outputEntry><text>""Declined (Poor Credit)""</text></outputEntry></rule>
      <rule><inputEntry><text>&gt;= 18</text></inputEntry><inputEntry><text>&lt; 30000</text></inputEntry><inputEntry><text>&gt;= 600</text></inputEntry><outputEntry><text>""Manual Review""</text></outputEntry></rule>
      <rule><inputEntry><text>&gt;= 18</text></inputEntry><inputEntry><text>&gt;= 30000</text></inputEntry><inputEntry><text>&gt;= 600</text></inputEntry><outputEntry><text>""Approved""</text></outputEntry></rule>
    </decisionTable>
  </decision>
</definitions>";

        private const string DefaultCsv =
@"Applicant Age,Applicant Income,Credit Score
17,50000,700
22,25000,650
30,50000,720
40,80000,580";
    }

    // ------------------------------------------------------------------------
    // Very small CSV helper (1 header row, rest data; all numeric parsed as double)
    // ------------------------------------------------------------------------
    internal static class CsvRecordParser
    {
        internal static List<IReadOnlyDictionary<string, object>> Parse(string csv)
        {
            var lines = csv.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
            if (lines.Length < 2) return new();

            string[] headers = lines[0].Split(',');
            var recs = new List<IReadOnlyDictionary<string, object>>();

            foreach (var line in lines.Skip(1))
            {
                var cells = line.Split(',');
                var dict = new Dictionary<string, object>(StringComparer.Ordinal);

                for (int i = 0; i < headers.Length && i < cells.Length; i++)
                {
                    string key = headers[i].Trim();
                    string raw = cells[i].Trim();

                    // try number
                    if (double.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out double d))
                        dict[key] = d;
                    else if (bool.TryParse(raw, out bool b))
                        dict[key] = b;
                    else
                        dict[key] = raw;
                }
                recs.Add(dict);
            }
            return recs;
        }
    }
}