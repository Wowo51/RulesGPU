//Copyright Warren Harding 2025.
using TorchSharp;
using static TorchSharp.torch;

namespace RulesGPUTest
{
    // Utility class to check for CUDA availability, keeping test logic cleaner.
    public static class TestUtils
    {
        public static bool IsCudaAvailable()
        {
            // This method directly checks for CUDA availability from TorchSharp.
            // It should be safe to call at any point after TorchSharp has been initialized.
            return cuda.is_available();
        }
    }
}