using Microsoft.Data.SqlClient;
using System.Linq;

namespace WebApplication1.Services
{
    public class UserService
    {
        public bool LoginUser(string username, string password)

        {

            string allowedSpecialCharacters = "!@#$%^&*?";

            if (!ValidationHelpers.IsValidInput(username) || !ValidationHelpers.IsValidInput(password, allowedSpecialCharacters))

                return false;

            string query = "SELECT COUNT(1) FROM Users WHERE Username = @Username AND Password = @Password";

            using (var connection = new SqlConnection("YourConnectionStringHere"))

            {

                using (var command = new SqlCommand(query, connection))

                {

                    command.Parameters.AddWithValue("@Username", username);

                    command.Parameters.AddWithValue("@Password", password);

                    connection.Open();

                    int count = (int)command.ExecuteScalar();

                    return count > 0;

                }

            }

        }

        public static bool IsValidXSSInput(string input)

        {

            if (string.IsNullOrEmpty(input))

                return true;

            //we dont want to allow input with <script or <iframe

            If((input.ToLower().contains(“< script”)) || (input.ToLower().contains(“< iframe”)))

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
