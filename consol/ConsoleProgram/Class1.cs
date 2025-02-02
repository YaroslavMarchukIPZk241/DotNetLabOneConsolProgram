using System;
using System.Data.SqlClient;


namespace BankLibrary
{

    public interface IOutput
    {
        void Write(string message);
    }
    public interface IInput
    {
        string Read();
    }
    interface IAutorization
    {
        bool Authenticate(string cardNumber, string pinCode, string Bank);

    }
    interface IBankOperation
    {

        double WithdrawalOfFundsInBank(string cardNumber, string pinCode, double moneyBank);
        double AddMoneyInYorCard(string cardNumber, string pinCode, double moneyBank);
    }
    interface IUserOperation
    {
        double ChekBalans(string cardNumber);
        double TransferringFundsToAnotherCard(string cardNumber, string cardAnotherCard, string pinCode);
    }
    public interface IDatabaseConnection
    {
        string GetConnectionString();
    }

    public class ConnectionManager : IDatabaseConnection
    {
        private readonly string _connectionString;

        public ConnectionManager(string connectionString)
        {
            _connectionString = connectionString;
        }

        public string GetConnectionString() => _connectionString;
    }

    public abstract class MessageHandler
    {
        public IOutput? Output { get; set; } = new ConsoleOutput();
        public IInput Input { get; set; } = new ConsoleInput();
        protected void DisplayMessage(string message)
        {
            Output?.Write(message);
        }
    }


    public class Account : MessageHandler, IUserOperation
    {
   
        private readonly IDatabaseConnection _connectionManager;
        public Account(IDatabaseConnection connectionManager)
        {
            _connectionManager = connectionManager;
        }

        private string GetInput() => Input.Read();

        public double ChekBalans(string cardNumber)
        {
            double balance = 0;
            using (SqlConnection connection = new SqlConnection(_connectionManager.GetConnectionString()))
            {
                string query = "SELECT Balance FROM Accounts WHERE CardNumber = @cardNumber";

                using (SqlCommand command = new SqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@cardNumber", cardNumber);
                    connection.Open();
                    object result = command.ExecuteScalar();

                    if (result != null)
                    {
                        balance = Convert.ToDouble(result);
                    }
                }
            }

            return balance;
        }

        public double TransferringFundsToAnotherCard(string cardNumber, string cardAnotherCard, string pinCode)
        {
            while (true)
            {
                DisplayMessage("Введіть суму, яку ви хочете переслати (введіть 0, якщо ви хочете вийти):");
                string input = GetInput();

                if (input == "0")
                {
                    break;
                }

                if (double.TryParse(input, out double prise) && prise > 0)
                {
                    double currentBalance = ChekBalans(cardNumber);

                    if (currentBalance >= prise)
                    {
                        string bankId = "";

                        using (SqlConnection connection = new SqlConnection(_connectionManager.GetConnectionString()))
                        {
                            string bankQuery = "SELECT BankId FROM Accounts WHERE CardNumber = @cardNumber";

                            using (SqlCommand bankCommand = new SqlCommand(bankQuery, connection))
                            {
                                bankCommand.Parameters.AddWithValue("@cardNumber", cardNumber);
                                connection.Open();
                                object result = bankCommand.ExecuteScalar();

                                if (result != null)
                                {
                                    bankId = result.ToString();
                                }
                            }
                        }

                        if (!string.IsNullOrEmpty(bankId))
                        {
                            using (SqlConnection connection = new SqlConnection(_connectionManager.GetConnectionString()))
                            {
                                string query = @"
                            BEGIN TRANSACTION;
                            UPDATE Accounts
                            SET Balance = Balance - @transferAmount
                            WHERE CardNumber = @cardNumber;                       
                            UPDATE Accounts
                            SET Balance = Balance + @transferAmount
                            WHERE CardNumber = @cardAnotherCard;

                            COMMIT TRANSACTION;";

                                using (SqlCommand command = new SqlCommand(query, connection))
                                {
                                    command.Parameters.AddWithValue("@cardNumber", cardNumber);
                                    command.Parameters.AddWithValue("@cardAnotherCard", cardAnotherCard);
                                    command.Parameters.AddWithValue("@transferAmount", prise);

                                    GetAccount getAccount = new GetAccount(_connectionManager);
                                    if (getAccount.Authenticate(cardNumber, pinCode, bankId))
                                    {
                                        connection.Open();
                                        command.ExecuteNonQuery();
                                        DisplayMessage("Переказ успішно завершено.");
                                        break;
                                    }
                                    else
                                    {
                                        DisplayMessage("Неправильний пін-код або банк не існує.");
                                        break;
                                    }
                                }
                            }
                        }
                        else
                        {
                            DisplayMessage("Банк не знайдено для зазначеного номера картки.");
                            break;
                        }
                    }
                    else
                    {
                        DisplayMessage("Недостатньо коштів на рахунку.");
                    }
                }
                else
                {
                    DisplayMessage("Будь ласка, введіть дійсну суму.");
                }
            }

            return 0;
        }
    }

    public class AutomatedTellerMachine : MessageHandler, IBankOperation
    {
          
    
        private readonly IDatabaseConnection _connectionManager;

        public AutomatedTellerMachine(IDatabaseConnection connectionManager)
        {
            _connectionManager = connectionManager;
        }

        private string GetInput() => Input.Read();

        public double AddMoneyInYorCard(string cardNumber, string pinCode, double moneyBank)
        {
            while (true)
            {
                DisplayMessage("Введіть суму, на яку ви хочете поповнити:");
                if (double.TryParse(GetInput(), out double sum))
                {
                    using var connection = new SqlConnection(_connectionManager.GetConnectionString());
                    connection.Open();

                    const string checkBalanceQuery = "SELECT Balance FROM Accounts WHERE CardNumber = @cardNumber";
                    using var checkCommand = new SqlCommand(checkBalanceQuery, connection);
                    checkCommand.Parameters.AddWithValue("@cardNumber", cardNumber);
                    var result = checkCommand.ExecuteScalar();

                    if (result != null && double.TryParse(result.ToString(), out double currentBalance))
                    {
                        const string query = "UPDATE Accounts SET Balance = Balance + @moneyBank WHERE CardNumber = @cardNumber";
                        using var command = new SqlCommand(query, connection);
                        command.Parameters.AddWithValue("@moneyBank", sum);
                        command.Parameters.AddWithValue("@cardNumber", cardNumber);

                        if (command.ExecuteNonQuery() > 0)
                        {
                            moneyBank += sum;
                            DisplayMessage($"Баланс успішно оновлено. Новий баланс: {currentBalance + sum}");
                            return sum;
                        }
                    }
                    DisplayMessage("Картка не знайдена.");
                }
                else
                {
                    DisplayMessage("Некоректний ввід. Будь ласка, введіть числове значення.");
                }
            }
        }

        public double WithdrawalOfFundsInBank(string cardNumber, string pinCode, double moneyBank)
        {
            while (true)
            {
                DisplayMessage("Введіть суму, яку ви хочете зняти:");
                if (double.TryParse(GetInput(), out double sum))
                {
                    if (moneyBank < sum)
                    {
                        DisplayMessage("Недостатньо грошей в банкоматі.");
                        return 0;
                    }

                    using var connection = new SqlConnection(_connectionManager.GetConnectionString());
                    connection.Open();
                    const string checkBalanceQuery = "SELECT Balance FROM Accounts WHERE CardNumber = @cardNumber";
                    using var checkCommand = new SqlCommand(checkBalanceQuery, connection);
                    checkCommand.Parameters.AddWithValue("@cardNumber", cardNumber);
                    var result = checkCommand.ExecuteScalar();

                    if (result != null && double.TryParse(result.ToString(), out double currentBalance))
                    {
                        if (currentBalance >= sum)
                        {
                            const string updateQuery = "UPDATE Accounts SET Balance = Balance - @moneyBank WHERE CardNumber = @cardNumber";
                            using var updateCommand = new SqlCommand(updateQuery, connection);
                            updateCommand.Parameters.AddWithValue("@moneyBank", sum);
                            updateCommand.Parameters.AddWithValue("@cardNumber", cardNumber);

                            if (updateCommand.ExecuteNonQuery() > 0)
                            {
                                moneyBank -= sum;
                                DisplayMessage($"Гроші успішно зняті. Новий баланс: {currentBalance - sum}");
                                return moneyBank;
                            }
                        }
                        else
                        {
                            DisplayMessage("Недостатньо коштів на картці.");
                        }
                    }
                    else
                    {
                        DisplayMessage("Картка не знайдена.");
                    }
                }
                else
                {
                    DisplayMessage("Некоректний ввід. Будь ласка, введіть числове значення.");
                }
            }
        }

       
    }

    public class GetAccount : MessageHandler, IAutorization
    {
        private readonly IDatabaseConnection _connectionManager;

        public GetAccount(IDatabaseConnection connectionManager)
        {
            _connectionManager = connectionManager;
        }

        public bool Authenticate(string cardNumber, string pinCode, string bank)
        {
            using var connection = new SqlConnection(_connectionManager.GetConnectionString());
            connection.Open();

            const string query = "SELECT COUNT(*) FROM Accounts WHERE CardNumber = @CardNumber AND PinCode = @PinCode AND BankId = @BankId";
            using var command = new SqlCommand(query, connection);
            command.Parameters.AddWithValue("@CardNumber", cardNumber);
            command.Parameters.AddWithValue("@PinCode", pinCode);
            command.Parameters.AddWithValue("@BankId", bank);

            int count = (int)command.ExecuteScalar();
            if (count > 0)
                return true;

            DisplayMessage("Ви ввели неправильні дані.");
            return false;
        }
    }

    public class Bank : MessageHandler
    {

        private readonly IDatabaseConnection _connectionManager;

        public Bank(IDatabaseConnection connectionManager)
        {
            _connectionManager = connectionManager;
        }

        private string GetInput() => Input.Read();

        public (string CardNumber, string PinCode) MenuBank()
        {
            DisplayMessage("Вітаю вас у консольному додатку ПТермінал\nВиберіть банк, з яким ви хочете працювати:");
            var banks = GetBanksFromDatabase();

            while (true)
            {
                for (int i = 0; i < banks.Count; i++)
                    DisplayMessage($"{i + 1} - {banks[i].Name}");

                if (int.TryParse(GetInput(), out int bankIndex) && bankIndex > 0 && bankIndex <= banks.Count)
                {
                    var selectedBank = banks[bankIndex - 1];
                    DisplayMessage($"Введіть номер картки для {selectedBank.Name}: ");
                    string cardNumber = GetInput();
                    DisplayMessage("Введіть пін-код: ");
                    string pinCode = GetInput();

                    if (int.TryParse(pinCode, out _))
                    {
                        var getAccount = new GetAccount(_connectionManager);
                        if (getAccount.Authenticate(cardNumber, pinCode, selectedBank.Id.ToString()))
                            return (cardNumber, pinCode);
                    }
                    else
                    {
                        DisplayMessage("Неправильний пін-код. Спробуйте ще раз.");
                    }
                }
                else
                {
                    DisplayMessage("Ви неправильно обрали банк.");
                }
            }
        }

        private List<BankParameter> GetBanksFromDatabase()
        {
            var banks = new List<BankParameter>();

            using var connection = new SqlConnection(_connectionManager.GetConnectionString());
            connection.Open();
            using var command = new SqlCommand("SELECT BankId, BankName FROM Banks", connection);
            using var reader = command.ExecuteReader();

            while (reader.Read())
            {
                banks.Add(new BankParameter { Id = reader.GetInt32(0), Name = reader.GetString(1) });
            }

            return banks;
        }
    }

    public class BankParameter
    {
        public int Id { get; set; }
        public string Name { get; set; }
    }
}