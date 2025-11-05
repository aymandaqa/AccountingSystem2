using System;
using AccountingSystem.Models;

namespace AccountingSystem.Extensions
{
    public static class AccountExtensions
    {
        /// <summary>
        /// Calculates the available balance for cash transactions.
        /// Treats debit-nature accounts as holding their current balance
        /// while credit-nature accounts are considered to have available
        /// balance only when their current balance reflects a net debit
        /// (i.e., negative credit balance).
        /// </summary>
        /// <param name="account">The account to inspect.</param>
        /// <returns>The available balance that can be used for cash payments.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the account is null.</exception>
        public static decimal GetAvailableCashBalance(this Account account)
        {
            if (account == null)
            {
                throw new ArgumentNullException(nameof(account));
            }

            decimal balance = account.Nature == AccountNature.Debit
                ? account.CurrentBalance
                : account.CurrentBalance * -1m;

            return balance > 0m ? balance : 0m;
        }

        /// <summary>
        /// Determines whether the account has enough balance to cover a cash payment.
        /// </summary>
        /// <param name="account">The account to inspect.</param>
        /// <param name="requiredAmount">The amount that needs to be covered.</param>
        /// <returns><c>true</c> when the available balance is sufficient; otherwise <c>false</c>.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the account is null.</exception>
        public static bool HasSufficientCashBalance(this Account account, decimal requiredAmount)
        {
            if (account == null)
            {
                throw new ArgumentNullException(nameof(account));
            }

            if (requiredAmount <= 0m)
            {
                return true;
            }

            var availableBalance = account.GetAvailableCashBalance();
            return availableBalance >= requiredAmount;
        }
    }
}

