using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ZDoc.SampleLib
{
    /// <summary>
    /// A repository abstraction used to exercise generic types, methods, properties,
    /// events, and XML doc features in ZDoc tests.
    /// </summary>
    /// <typeparam name="T">The entity type stored by the repository.</typeparam>
    public class Repository<T> where T : class
    {
        private readonly List<T> _items = new List<T>();

        /// <summary>Raised after an item is added.</summary>
        public event EventHandler<T>? ItemAdded;

        /// <summary>Gets the number of items currently stored.</summary>
        public int Count => _items.Count;

        /// <summary>Gets or sets the item at the given index.</summary>
        /// <param name="index">Zero-based position.</param>
        /// <returns>The item at <paramref name="index"/>.</returns>
        /// <exception cref="ArgumentOutOfRangeException">If <paramref name="index"/> is invalid.</exception>
        public T this[int index]
        {
            get => _items[index];
            set => _items[index] = value;
        }

        /// <summary>Adds an item and raises <see cref="ItemAdded"/>.</summary>
        /// <param name="item">The item to add.</param>
        public void Add(T item)
        {
            _items.Add(item);
            ItemAdded?.Invoke(this, item);
        }

        /// <summary>Finds the first item matching a predicate.</summary>
        /// <param name="predicate">The match test.</param>
        /// <returns>The matching item, or <c>null</c> if none match.</returns>
        public T? Find(Func<T, bool> predicate)
        {
            foreach (var item in _items)
                if (predicate(item)) return item;
            return null;
        }

        /// <summary>Asynchronously loads items from a source.</summary>
        /// <param name="source">The async source.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>The number of items loaded.</returns>
        public async Task<int> LoadAsync(
            IEnumerable<T> source,
            CancellationToken cancellationToken = default)
        {
            int count = 0;
            foreach (var item in source)
            {
                cancellationToken.ThrowIfCancellationRequested();
                Add(item);
                count++;
                await Task.Yield();
            }
            return count;
        }
    }

    /// <summary>Severity levels for a log entry.</summary>
    public enum Severity
    {
        /// <summary>Diagnostic detail.</summary>
        Debug = 0,
        /// <summary>Informational message.</summary>
        Info = 1,
        /// <summary>A recoverable problem.</summary>
        Warning = 2,
        /// <summary>A failure.</summary>
        Error = 3,
    }

    /// <summary>Handles a formatted log message.</summary>
    /// <param name="severity">The message severity.</param>
    /// <param name="message">The formatted text.</param>
    public delegate void LogHandler(Severity severity, string message);

    /// <summary>
    /// A small immutable money value, used to exercise structs, operators, and overloads.
    /// </summary>
    public readonly struct Money
    {
        /// <summary>Creates a money value.</summary>
        /// <param name="amount">The numeric amount.</param>
        /// <param name="currency">ISO currency code, e.g. <c>USD</c>.</param>
        public Money(decimal amount, string currency)
        {
            Amount = amount;
            Currency = currency;
        }

        /// <summary>The numeric amount.</summary>
        public decimal Amount { get; }

        /// <summary>The ISO currency code.</summary>
        public string Currency { get; }

        /// <summary>Adds two money values of the same currency.</summary>
        /// <param name="left">Left operand.</param>
        /// <param name="right">Right operand.</param>
        /// <returns>The summed value.</returns>
        public static Money operator +(Money left, Money right) =>
            new Money(left.Amount + right.Amount, left.Currency);

        /// <summary>Converts the amount to a plain decimal.</summary>
        /// <param name="money">The value to convert.</param>
        public static explicit operator decimal(Money money) => money.Amount;
    }

    /// <summary>Defines a named operation that can be validated.</summary>
    public interface IOperation
    {
        /// <summary>The operation's unique name.</summary>
        string Name { get; }

        /// <summary>Validates the operation's current state.</summary>
        /// <returns><c>true</c> if valid.</returns>
        bool Validate();
    }

    /// <summary>
    /// A static utility holder demonstrating static classes, constants, and nested types.
    /// </summary>
    public static class Constants
    {
        /// <summary>The maximum supported batch size.</summary>
        public const int MaxBatchSize = 1000;

        /// <summary>A nested options record.</summary>
        public sealed class Options
        {
            /// <summary>Whether verbose output is enabled.</summary>
            public bool Verbose { get; set; }
        }
    }
}
