using System.Linq;

namespace WebApplication1.Services
{
    public class XssService
    {
        public static bool IsValidXSSInput(string input)
        {

            if (string.IsNullOrEmpty(input))

                return true;

            //we dont want to allow input with <script or <iframe

            if ((input.ToLower().Contains("< script")) || (input.ToLower().Contains("< iframe")))
                return false;

            return true;
        }

        public void TestXssInput()
        {

            string maliciousInput = "<script>alert('XSS');</script>";

            bool isValid = IsValidXSSInput(maliciousInput);

            Console.WriteLine(isValid ? "XSS Test Failed" : "XSS Test Passed");

        }
    }
}
