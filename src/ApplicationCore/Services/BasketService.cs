// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

using Contoso.FraudProtection.ApplicationCore.Interfaces;
using System.Threading.Tasks;
using System.Collections.Generic;
using Contoso.FraudProtection.ApplicationCore.Specifications;
using Contoso.FraudProtection.ApplicationCore.Entities;
using System.Linq;
using Ardalis.GuardClauses;
using Contoso.FraudProtection.ApplicationCore.Entities.BasketAggregate;

namespace Contoso.FraudProtection.ApplicationCore.Services
{
    public class BasketService : IBasketService
    {
        private readonly IAsyncRepository<Basket> _basketRepository;
        private readonly IUriComposer _uriComposer;
        private readonly IAppLogger<BasketService> _logger;
        private readonly IRepository<CatalogItem> _itemRepository;

        public BasketService(IAsyncRepository<Basket> basketRepository,
            IRepository<CatalogItem> itemRepository,
            IUriComposer uriComposer,
            IAppLogger<BasketService> logger)
        {
            _basketRepository = basketRepository;
            _uriComposer = uriComposer;
            _logger = logger;
            _itemRepository = itemRepository;
        }

        public async Task AddItemToBasket(int basketId, int catalogItemId, decimal price, int quantity)
        {
            var basket = await _basketRepository.GetByIdAsync(basketId);

            basket.AddItem(catalogItemId, price, quantity);

            await _basketRepository.UpdateAsync(basket);
        }

        public async Task DeleteBasketAsync(int basketId)
        {
            var basket = await _basketRepository.GetByIdAsync(basketId);

            await _basketRepository.DeleteAsync(basket);
        }

        public async Task<int> GetBasketItemCountAsync(string userName)
        {
            Guard.Against.NullOrEmpty(userName, nameof(userName));
            var basket = await GetBasketByUsername(userName);
            if (basket == null)
            {
                _logger.LogInformation($"No basket found for {userName}");
                return 0;
            }
            int count = basket.Items.Sum(i => i.Quantity);
            _logger.LogInformation($"Basket for {userName} has {count} items.");
            return count;
        }

        private async Task<Basket> GetBasketByUsername(string userName)
        {
            var basketSpec = new BasketWithItemsSpecification(userName);
            return (await _basketRepository.ListAsync(basketSpec)).FirstOrDefault();
        }

        public async Task SetQuantities(int basketId, Dictionary<string, int> quantities)
        {
            Guard.Against.Null(quantities, nameof(quantities));
            var basket = await _basketRepository.GetByIdAsync(basketId);
            Guard.Against.NullBasket(basketId, basket);

            var itemsToDelete = new List<BasketItem>();
            foreach (var item in basket.Items)
            {
                if (quantities.TryGetValue(item.Id.ToString(), out var quantity))
                {
                    if (quantity == 0)
                    {
                        _logger.LogInformation($"Deleting item ID: {item.Id} from basket ID: {basketId}.");
                        itemsToDelete.Add(item);
                        continue;
                    }
                    _logger.LogInformation($"Updating quantity of item ID: {item.Id} to {quantity}.");
                    item.Quantity = quantity;
                }
            }

            foreach (var item in itemsToDelete)
            {
                basket.RemoveItem(item);
            }

            await _basketRepository.UpdateAsync(basket);
        }

        public async Task TransferBasketAsync(string anonymousId, string userName)
        {
            Guard.Against.NullOrEmpty(anonymousId, nameof(anonymousId));
            Guard.Against.NullOrEmpty(userName, nameof(userName));

            var anonymousBasket = await GetBasketByUsername(anonymousId);
            if (anonymousBasket == null)
                return;

            var existingUserBasket = await GetBasketByUsername(userName);
            if (existingUserBasket == null)
            {
                anonymousBasket.BuyerId = userName;
                await _basketRepository.UpdateAsync(anonymousBasket);
                return;
            }

            MergeBaskets(existingUserBasket, anonymousBasket);

            await _basketRepository.UpdateAsync(existingUserBasket);
            await _basketRepository.DeleteAsync(anonymousBasket);
        }

        /// <summary>
        /// Merges items from <paramref name="anonymousBasket"/> into <paramref name="existingUserBasket"/>.
        /// </summary>
        private void MergeBaskets(Basket existingUserBasket, Basket anonymousBasket)
        {
            foreach (var item in anonymousBasket.Items)
            {
                existingUserBasket.AddItem(item.CatalogItemId, item.UnitPrice, item.Quantity);
            }
        }
    }
}
