﻿using System.Data.SqlClient;

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
        double withdrawalOfFundsInBank(string cardNumber, string pinCode,double moneyBank);
        double AddMoneyInYorCard(string cardNumber, string pinCode,double moneyBank);
    }

    interface IUserOperation
    {
        double ChekBalans(string cardNumber);
        double TransferringFundsToAnotherCard(string cardNumber, string cardAnotherCard, string pinCode);
    }

    public class Account : IUserOperation
    {  
        public IInput Input { get; set; } = new ConsoleInput();

        public string GetInput()
        {
            return Input.Read();
        }

        public IOutput? Output { get; set; } = new ConsoleOutput();

        public void DisplayMessage(string message)
        {
            Output?.Write(message);
        }

        private ConnectionManager connectionManager;

        public Account(ConnectionManager connectionManager)
        {
            this.connectionManager = connectionManager;
        }

        public double ChekBalans(string cardNumber)
        {
            double balance = 0;
            using (SqlConnection connection = new SqlConnection(connectionManager.GetConnectionString()))
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


                        using (SqlConnection connection = new SqlConnection(connectionManager.GetConnectionString()))
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
                            using (SqlConnection connection = new SqlConnection(connectionManager.GetConnectionString()))
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

                                    GetAccount getAccount = new GetAccount(connectionManager);
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

    public class AutomatedTellerMachine : IBankOperation
    {
        public IInput Input { get; set; } = new ConsoleInput();
        public string GetInput()
        {
            return Input.Read();
        }
        private ConnectionManager connectionManager;
        public IOutput? Output { get; set; } = new ConsoleOutput();
        public void DisplayMessage(string message)
        {
            Output?.Write(message);
        }
        public AutomatedTellerMachine(ConnectionManager connectionManager)
        {
            this.connectionManager = connectionManager;
        }

        public double AddMoneyInYorCard(string cardNumber, string pinCode, double moneyBank) 
        {
            while (true)
            {
                DisplayMessage("Введіть суму, на яку ви хочете поповнити:");
                string input = GetInput();

                if (double.TryParse(input, out double sum))
                {
                    double currentBalance = GetCurrentBalance(cardNumber);
                    if (currentBalance >= 0)
                    {
                        moneyBank = UpdateBalance(cardNumber, sum, moneyBank);
                        DisplayMessage("Баланс успішно оновлено.");
                        DisplayMessage("Гроші успішно поповнені. Ваш новий баланс: " + (currentBalance + sum));
                        DisplayMessage("Нова сума в банкоматі: " + moneyBank); 
                        return sum;
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

        private double GetCurrentBalance(string cardNumber)
        {
            double currentBalance = -1;
            using (SqlConnection connection = new SqlConnection(connectionManager.GetConnectionString()))
            {
                connection.Open();
                string checkBalanceQuery = "SELECT Balance FROM Accounts WHERE CardNumber = @cardNumber";
                using (var checkCommand = new SqlCommand(checkBalanceQuery, connection))
                {
                    checkCommand.Parameters.AddWithValue("@cardNumber", cardNumber);
                    var result = checkCommand.ExecuteScalar();
                    if (result != null && double.TryParse(result.ToString(), out currentBalance))
                    {
                        return currentBalance;
                    }
                }
            }
            return -1;
        }

        private double UpdateBalance(string cardNumber, double sum, double moneyBank)
        {
            using (SqlConnection connection = new SqlConnection(connectionManager.GetConnectionString()))
            {
                connection.Open();
                string query = "UPDATE Accounts SET Balance = Balance + @moneyBank WHERE CardNumber = @cardNumber";
                using (var command = new SqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@moneyBank", sum);
                    command.Parameters.AddWithValue("@cardNumber", cardNumber);
                    int rowsAffected = command.ExecuteNonQuery();
                    if (rowsAffected > 0)
                    {
                        moneyBank += sum;
                    }
                    else
                    {
                        DisplayMessage("Картка не знайдена.");
                    }
                }
            }

            return moneyBank;
        }

        public double withdrawalOfFundsInBank(string cardNumber, string pinCode, double moneyBank)
        {
            while (true)
            {
                DisplayMessage("Введіть суму, яку ви хочете зняти:");
                string input = GetInput();

                if (double.TryParse(input, out double sum))
                {
                    if (moneyBank < sum)
                    {
                        DisplayMessage("Недостатньо грошей в банкоматі.");
                        return 0; 
                    }

                    double currentBalance = GetCurrentBalance(cardNumber);
                    if (currentBalance >= 0)
                    {
                        if (currentBalance >= sum)
                        {
                            moneyBank = WithdrawFunds(cardNumber, sum, moneyBank);
                            DisplayMessage("Гроші успішно зняті. Ваш новий баланс: " + (currentBalance - sum));
                            return moneyBank;
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

        private double WithdrawFunds(string cardNumber, double sum, double moneyBank)
        {
            using (SqlConnection connection = new SqlConnection(connectionManager.GetConnectionString()))
            {
                connection.Open();
                string updateQuery = "UPDATE Accounts SET Balance = Balance - @moneyBank WHERE CardNumber = @cardNumber";
                using (var updateCommand = new SqlCommand(updateQuery, connection))
                {
                    updateCommand.Parameters.AddWithValue("@moneyBank", sum);
                    updateCommand.Parameters.AddWithValue("@cardNumber", cardNumber);
                    int rowsAffected = updateCommand.ExecuteNonQuery();
                    if (rowsAffected > 0)
                    {
                        moneyBank -= sum;
                    }
                    else
                    {
                        DisplayMessage("Картка не знайдена.");
                    }
                }
            }
            return moneyBank;
        }
            
    }

    public class ConnectionManager
    {
        private string connectionString;

        public ConnectionManager(string connectionString)
        {
            this.connectionString = connectionString;

        }

        public string GetConnectionString()
        {
            return connectionString;
        }
    }

    public class GetAccount : IAutorization 
    {
        public IOutput? Output { get; set; } = new ConsoleOutput();
        public void DisplayMessage(string message)
        {
            Output?.Write(message);
        }
        private ConnectionManager connectionManager;

        public GetAccount(ConnectionManager connectionManager)
        {
            this.connectionManager = connectionManager;
        }

        public bool Authenticate(string cardNumber, string pinCode, string bank)
        {
            using (SqlConnection connection = new SqlConnection(connectionManager.GetConnectionString()))
            {
                connection.Open();
                string query = "SELECT COUNT(*) FROM Accounts WHERE CardNumber = @CardNumber AND PinCode = @PinCode AND BankId = @BankId";

                using (SqlCommand command = new SqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@CardNumber", cardNumber);
                    command.Parameters.AddWithValue("@PinCode", pinCode);
                    command.Parameters.AddWithValue("@BankId", bank);

                    int count = (int)command.ExecuteScalar();
                    if (count > 0)
                    {

                        return true;

                    }
                    else
                    {
                        DisplayMessage("Ви ввели неправильні дані.");
                        return false;
                    }
                }

            }
        }
    }

    public class Bank 
    {
        public IOutput? Output { get; set; } = new ConsoleOutput();
        public IInput Input { get; set; } = new ConsoleInput();
        private ConnectionManager connectionManager;
        public string GetInput()
        {
            return Input.Read();
        }

        public void DisplayMessage(string message)
        {
            Output?.Write(message);
        }

        public Bank(ConnectionManager connectionManager)
        {
            this.connectionManager = connectionManager;
        }

        public string[] MenuBank()
        {
            DisplayMessage("Вітаю вас у консольному додатку ПТермінал\nВиберіть банк, з яким ви хочете працювати:");
            List<BankParameter> banks = GetBanksFromDatabase();
            
            while (true)
            {
                DisplayBankList(banks);
                string choix = GetInput();

                int bankIndex = ParseBankChoice(choix, banks);
                if (bankIndex != -1)
                {
                    var selectedBank = banks[bankIndex];
                    string cardNumber = GetCardNumber(selectedBank);
                    string pinCodeInput = GetPinCode();

                    if (ValidateCardAndPin(cardNumber, pinCodeInput, selectedBank))
                    {
                        return new string[] { cardNumber, pinCodeInput };
                    }
                }
                else
                {
                    DisplayMessage("Ви неправильно обрали банк.");
                }
            }
        }

        private void DisplayBankList(List<BankParameter> banks)
        {
            for (int i = 0; i < banks.Count; i++)
            {
                DisplayMessage($"{i + 1} - {banks[i].Name}");
            }
        }

        private int ParseBankChoice(string choix, List<BankParameter> banks)
        {
            if (int.TryParse(choix, out int bankIndex) && bankIndex > 0 && bankIndex <= banks.Count)
            {
                return bankIndex - 1;
            }

            return -1;
        }

        private string GetCardNumber(BankParameter selectedBank)
        {
            DisplayMessage($"Введіть номер картки для {selectedBank.Name}: ");
            return GetInput();
        }

        private string GetPinCode()
        {
            DisplayMessage("Введіть пін-код: ");
            return GetInput();
        }

        private bool ValidateCardAndPin(string cardNumber, string pinCodeInput, BankParameter selectedBank)
        {
            int pinCode;
            if (int.TryParse(pinCodeInput, out pinCode))
            {
                GetAccount getAccount = new GetAccount(connectionManager);
                return getAccount.Authenticate(cardNumber, pinCodeInput, selectedBank.Id.ToString());
            }
            else
            {
                DisplayMessage("Неправильний пін-код. Спробуйте ще раз.");
                return false;
            }
        }
        

        public List<BankParameter> GetBanksFromDatabase()
        {
            List<BankParameter> banks = new List<BankParameter>();

            using (SqlConnection connection = new SqlConnection(connectionManager.GetConnectionString()))
            {
                connection.Open();
                SqlCommand command = new SqlCommand("SELECT BankId, BankName FROM Banks", connection);
                SqlDataReader reader = command.ExecuteReader();

                while (reader.Read())
                {
                    BankParameter bank = new BankParameter();
                    bank.Id = reader.GetInt32(0);
                    bank.Name = reader.GetString(1);
                    banks.Add(bank);
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
}
