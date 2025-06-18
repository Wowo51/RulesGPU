//Copyright Warren Harding 2025.
using RulesDMN;
using RulesDMN.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using TorchSharp;
using static TorchSharp.torch;

namespace RulesGPU
{
    /// <summary>
    /// A facade for the RulesGPU library, providing an easy-to-use interface
    /// for loading DMN decision tables and evaluating them on the GPU.
    /// </summary>
    public sealed class DmnGpuEngine : IDisposable
    {
        private readonly Device _device;
        private GpuDecisionTableRepresentation? _gpuData;
        private readonly RulesGPUEngine _rulesGpuEngine;

        /// <summary>
        /// Initializes a new instance of the <see cref="DmnGpuEngine"/> class.
        /// </summary>
        /// <param name="device">The TorchSharp device to use for computation (e.g., CPU, CUDA).</param>
        public DmnGpuEngine(Device device)
        {
            _device = device;
            _rulesGpuEngine = new RulesGPUEngine(device);
        }

        /// <summary>
        /// Loads a DMN decision table from an XML string and prepares it for GPU evaluation.
        /// Only the first decision table found in the DMN model will be processed.
        /// </summary>
        /// <param name="dmnXml">The DMN XML string.</param>
        /// <returns>True if a decision table was successfully loaded and converted, false otherwise.</returns>
        public bool LoadDmnDecisionTable(string dmnXml)
        {
            _gpuData?.Dispose();
            _gpuData = null;

            DmnModel? dmnModel = DmnParser.ParseDmn(dmnXml);
            if (dmnModel is null)
            {
                return false;
            }

            DecisionTable? decisionTable = dmnModel.Decisions
                                                  .Select(d => d.DecisionLogic)
                                                  .OfType<DecisionTable>()
                                                  .FirstOrDefault();

            if (decisionTable is null)
            {
                return false;
            }

            _gpuData = DmnToGpuConverter.ConvertDecisionTableToGpuRepresentation(decisionTable, _device);
            return _gpuData is not null;
        }

        /// <summary>
        /// Evaluates a single input record against the loaded DMN decision table on the GPU.
        /// </summary>
        /// <param name="inputs">A dictionary of input variable names and their corresponding values.</param>
        /// <returns>
        /// A dictionary of output variable names and their values if a single output is expected (e.g., Unique, First hit policies),
        /// or a list of such dictionaries if multiple outputs are aggregated (e.g., Collect hit policy).
        /// Returns null if no rule matches and hit policy requires a single match (Unique/First), or if evaluation fails or no DMN is loaded.
        /// </returns>
        public object? Evaluate(IReadOnlyDictionary<string, object> inputs)
        {
            if (_gpuData is null)
            {
                return null;
            }
            return _rulesGpuEngine.Evaluate(_gpuData, inputs);
        }

        /// <summary>
        /// Evaluates a list of input records against the loaded DMN decision table in parallel on the GPU.
        /// </summary>
        /// <param name="inputRecords">A list of dictionaries, where each dictionary represents one set of input data.</param>
        /// <returns>
        /// A list of objects, where each object is the result for the corresponding input record.
        /// An object can be a dictionary of output values (for Unique, First) or a list of dictionaries (for Collect).
        /// Returns null for a given record if no rule matches or if evaluation fails for that record, or if no DMN is loaded.
        /// </returns>
        public IReadOnlyList<object?> Evaluate(IReadOnlyList<IReadOnlyDictionary<string, object>> inputRecords)
        {
            if (_gpuData is null)
            {
                List<object?> emptyResults = new List<object?>();
                for (int i = 0; i < inputRecords.Count; i++)
                {
                    emptyResults.Add(null); 
                }
                return emptyResults;
            }
            return _rulesGpuEngine.Evaluate(_gpuData, inputRecords);
        }

        /// <summary>
        /// Releases all resources used by the <see cref="DmnGpuEngine"/>.
        /// </summary>
        public void Dispose()
        {
            _gpuData?.Dispose();
            // This object has no unmanaged resources or other managed disposable components
            // that are not already handled by _gpuData's disposal or garbage collection.
            GC.SuppressFinalize(this);
        }
    }
}