// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

using Contoso.FraudProtection.Web.Services;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace Contoso.FraudProtection.Web.Controllers.Api
{
    public class CatalogController : BaseApiController
    {
        private readonly ICatalogService _catalogService;

        public CatalogController(ICatalogService catalogService) => _catalogService = catalogService;

        [HttpGet]
        public async Task<IActionResult> List(int? brandFilterApplied, int? typesFilterApplied, int? page)
        {
            var itemsPage = 10;           
            var catalogModel = await _catalogService.GetCatalogItems(page ?? 0, itemsPage, brandFilterApplied, typesFilterApplied);
            return Ok(catalogModel);
        }
    }
}
