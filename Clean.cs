using Com.Clout2.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging; 
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Com.Clout.Api.Models.Models;
using System.Net.Http;

namespace Com.Clout2.Api.Controllers
{
    /// <summary>
    /// Addresses RESTful API Controller (ASP.NET Core)
    /// </summary>
    [ApiExplorerSettings(IgnoreApi = false)]
    public class AddressApiController : AddressBaseApiController
    {
        /// <summary>
        /// Address API Controller
        /// </summary>
        /// <param name="apiDataContext">DbContext for EntityFramework</param>
        /// <param name="logger">Injected ILoggerFactory</param>
        public AddressApiController(ApiDataContext apiDataContext, ILoggerFactory logger) : base(apiDataContext, logger)
        { 
        }

        /// <summary>
        /// Update Address in the datastore
        /// </summary>
        /// <param name="loginToken">The token for the user requesting this data. 
        ///	 If provided, the system verifies the user rights to access the data</param>
        /// <param name="id"></param>
        /// <param name="fullAddress">Single string containing street address, city, state, zip</param>
        /// <response code="200">successful operation</response>
        /// <response code="405">Invalid input</response>
        [HttpPut]
        [Route("/address/clean")]
        [ProducesResponseType(typeof(Address), 200)]
        [ProducesResponseType(typeof(IDictionary<string, string>), 400)]
        public async virtual Task<IActionResult> CleanAddress(
            [FromQuery]string loginToken,
            [FromQuery]long? id,
            [FromQuery]string fullAddress)
        {
            if (string.IsNullOrEmpty(loginToken)) return BadRequest("Login token is required");
            var loggedInUser = _tokenizer.ValidateToken(loginToken);
            if (loggedInUser != null)
            {
                var itemToUpdate = _dbContext.Addresses.Single(b => b.Id == id);
                if (itemToUpdate != null)
                {
                    if (String.IsNullOrEmpty(fullAddress))
                    {
                        if (String.IsNullOrEmpty(itemToUpdate.FullAddress))
                        {
                            fullAddress = itemToUpdate.AddressLine1 + ", "
                                + itemToUpdate.City + " " + itemToUpdate.State + " " + itemToUpdate.Zipcode;
                        }
                        else
                        {
                            fullAddress = itemToUpdate.FullAddress;
                        }
                    }
                    itemToUpdate.FullAddress = fullAddress.Substring(0, fullAddress.Count() < 255 ? fullAddress.Count() : 255);

                    using (var client = new HttpClient())
                    {
                        try
                        {
                            client.BaseAddress = new Uri("https://us-street.api.smartystreets.com");
                            var response = await client.GetAsync($"/street-address?auth-id=f11a581a-b69d-44ba-8e4d-79e15e8e9740&auth-token=v7i3yiFgfNEVwAX0T4HF&candidates=1&street=" + fullAddress);
                            response.EnsureSuccessStatusCode();
                            var stringResult = await response.Content.ReadAsStringAsync();


                            // smarty streets is returning array. grabbing first one for now.
                            var smartyStreetArray = JsonConvert.DeserializeObject<List<SmartyStreetValidAddress>>(stringResult);
                            if (smartyStreetArray != null && smartyStreetArray.Count() > 0)
                            {
                                var smartyStreetChosenOne = smartyStreetArray[0];

                                if (!String.IsNullOrEmpty(smartyStreetChosenOne.delivery_line_1)) itemToUpdate.AddressLine1 = smartyStreetChosenOne.delivery_line_1;
                                if (!String.IsNullOrEmpty(smartyStreetChosenOne.components.city_name)) itemToUpdate.City = smartyStreetChosenOne.components.city_name;
                                if (!String.IsNullOrEmpty(smartyStreetChosenOne.components.state_abbreviation)) itemToUpdate.State = smartyStreetChosenOne.components.state_abbreviation;
                                if (!String.IsNullOrEmpty(smartyStreetChosenOne.components.zipcode)) itemToUpdate.Zipcode = smartyStreetChosenOne.components.zipcode;
                            }

                            _dbContext.SaveChanges();
                            return Ok(itemToUpdate);
                        }
                        catch (HttpRequestException httpRequestException)
                        {
                            return BadRequest($"Error getting weather from OpenWeather: {httpRequestException.Message}");
                        }
                    }
                }
                else return NotFound("Address not found");
            }
            else return BadRequest("Invalid or expired login token");
        }
    }
}
