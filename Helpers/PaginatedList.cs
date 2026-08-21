using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace TisaWasteManagement.Models
{
    // This helper class handles pagination for the Sitio list
    // It takes a list of items, a page number, and page size, and returns only the items for that page
    public class PaginatedList<T> : List<T>
    {
        // The current page number (starting from 1)
        public int PageIndex { get; private set; }

        // Total number of pages available
        public int TotalPages { get; private set; }

        // Total number of items in the entire list
        public int TotalItems { get; private set; }

        // Constructor - creates a paginated list with the specified items and pagination info
        public PaginatedList(List<T> items, int count, int pageIndex, int pageSize)
        {
            PageIndex = pageIndex;
            TotalPages = (int)Math.Ceiling(count / (double)pageSize);
            TotalItems = count;

            // Add the items for the current page to the list
            this.AddRange(items);
        }

        // Returns true if there is a previous page to go to
        public bool HasPreviousPage => PageIndex > 1;

        // Returns true if there is a next page to go to
        public bool HasNextPage => PageIndex < TotalPages;

        // Calculates the index of the first item on the current page
        public int FirstItemOnPage => (PageIndex - 1) * PageSize + 1;

        // Calculates the index of the last item on the current page
        public int LastItemOnPage => Math.Min(PageIndex * PageSize, TotalItems);

        // The number of items per page (needed for calculations)
        public int PageSize { get; private set; }

        // Static method that creates a paginated list asynchronously
        // This is the main method called from the controller to get paginated data
        public static async Task<PaginatedList<T>> CreateAsync(IQueryable<T> source, int pageIndex, int pageSize)
        {
            // First, get the total count of all items
            var count = await source.CountAsync();

            // Then, skip items from previous pages and take only the items for the current page
            // Skip: (pageIndex - 1) * pageSize items from previous pages
            // Take: pageSize items for the current page
            var items = await source.Skip((pageIndex - 1) * pageSize)
                                   .Take(pageSize)
                                   .ToListAsync();

            // Return a new paginated list with the items and pagination info
            return new PaginatedList<T>(items, count, pageIndex, pageSize);
        }
    }
}