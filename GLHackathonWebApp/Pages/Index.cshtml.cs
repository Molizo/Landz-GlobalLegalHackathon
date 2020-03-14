using System;
using CsvHelper;
using GeoJSON.Net.Feature;
using GeoJSON.Net.Geometry;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using GoogleApi;
using GoogleApi.Entities.Common;
using GoogleApi.Entities.Common.Enums;
using GoogleApi.Entities.Places.Details.Request;
using GoogleApi.Entities.Places.Photos.Request;
using GoogleApi.Entities.Places.Search.NearBy.Request;
using System.Threading.Tasks;
using MaxMind.GeoIP2;
using Location = GoogleApi.Entities.Maps.Roads.Common.Location;

namespace GLHackathonWebApp.Pages
{
    public class PropertyDetail
    {
        public string FormattedAddress { get; set; }
        public string PhoneNumber { get; set; }
        public string Photo { get; set; }
        public string PhotoCredit { get; set; }
        public string Website { get; set; }
    }

    public class IndexModel : PageModel
    {
        private readonly ILogger<IndexModel> _logger;
        private readonly IHostingEnvironment _hostingEnvironment;
        public string MapboxAccessToken { get; }
        private readonly IConfiguration _config;
        public string GoogleApiKey { get; }
        public double InitialLatitude { get; set; } = 0;
        public double InitialLongitude { get; set; } = 0;
        public int InitialZoom { get; set; } = 1;

        public IndexModel(ILogger<IndexModel> logger, IConfiguration config, IHostingEnvironment hostingEnvironment)
        {
            _logger = logger;
            _config = config;
            _hostingEnvironment = hostingEnvironment;
            MapboxAccessToken = _config["Mapbox:AccessToken"];
            GoogleApiKey = _config["Google:ApiKey"];
        }

        public void OnGet()
        {
            try
            {
                using (var reader = new DatabaseReader(_hostingEnvironment.WebRootPath + "\\GeoLite2-City.mmdb"))
                {
                    // Determine the IP Address of the request
                    var ipAddress = HttpContext.Connection.RemoteIpAddress;
                    // Get the city from the IP Address
                    var city = reader.City(ipAddress);

                    if (city?.Location?.Latitude != null && city?.Location?.Longitude != null)
                    {
                        InitialLatitude = city.Location.Latitude.Value;
                        InitialLongitude = city.Location.Longitude.Value;
                        InitialZoom = 11;
                    }
                }
            }
            catch (Exception e)
            {
                // Just suppress errors. If we could not retrieve the location for whatever reason
                // there is not reason to notify the user. We'll just simply not know their current
                // location and won't be able to center the map on it
            }
        }

        public IActionResult OnGetPlots()
        {
            using (var reader = new StreamReader(_hostingEnvironment.ContentRootPath + "/Properties.csv"))
            using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
            {
                FeatureCollection featureCollection = new FeatureCollection();

                int cnt = 0;

                while (csv.Read())
                {
                    cnt++;

                    double latitude = csv.GetField<double>(0);
                    double longitude = csv.GetField<double>(1);
                    string name = csv.GetField<string>(2);

                    string technicalUsefulSurface = csv.GetField<string>(3);
                    string technicalBuiltSurface = csv.GetField<string>(4);
                    string technicalBuildingAccessFootpath = csv.GetField<string>(5);
                    string technicalBuildingAccessAutomobiles = csv.GetField<string>(6);
                    string technicalBuildingStructure = csv.GetField<string>(7);
                    string technicalCommunalSpaces = csv.GetField<string>(8);
                    string technicalExternalSpaces = csv.GetField<string>(9);
                    string technicalBuildingPadding = csv.GetField<string>(10);
                    string technicalMaxHeight = csv.GetField<string>(11);
                    string technicalUtilities = csv.GetField<string>(12);

                    if (technicalBuildingStructure == "Not available")
                        technicalBuildingStructure = "P";
                    if (!technicalBuildingStructure.Contains('M'))
                        technicalBuildingStructure += "+0M";
                    if (!technicalBuildingStructure.Contains('E'))
                        technicalBuildingStructure += "+0E";
                    if (!technicalBuildingStructure.Contains('D'))
                        technicalBuildingStructure += "+0D";
                    if (!technicalBuildingStructure.Contains('S'))
                        technicalBuildingStructure += "+0S";

                    featureCollection.Features.Add(new Feature(
                        new Point(new Position(latitude, longitude)),
                        new Dictionary<string, object>
                        {
                            {"id", cnt},
                            {"name", name},
                            {"technicalUsefulSurface", technicalUsefulSurface},
                            {"technicalBuiltSurface", technicalBuiltSurface},
                            {"technicalBuildingAccessFootpath", technicalBuildingAccessFootpath},
                            {"technicalBuildingAccessAutomobiles", technicalBuildingAccessAutomobiles},
                            {"technicalBuildingStructure",technicalBuildingStructure },
                            {"technicalCommunalSpaces", technicalCommunalSpaces},
                            {"technicalExternalSpaces",technicalExternalSpaces },
                            {"technicalBuildingPadding",technicalBuildingPadding },
                            {"technicalMaxHeight", technicalMaxHeight},
                            {"technicalUtilities", technicalUtilities},
                            {"mansarda",technicalBuildingStructure.Substring(technicalBuildingStructure.IndexOf('M')-1,1)+"0" },
                            {"etaj",technicalBuildingStructure.Substring(technicalBuildingStructure.IndexOf('E')-1,1)+"0" },
                            {"demisol","0" },
                            {"subsol",technicalBuildingStructure.Substring(technicalBuildingStructure.IndexOf('S')-1,1)+"0" }
                        }));
                }

                return new JsonResult(featureCollection);
            }
        }

        public async Task<IActionResult> OnGetPlotDetails(string name, double latitude, double longitude)
        {
            var propertyDetail = new PropertyDetail();

            // Execute the search request
            var searchResponse = await GooglePlaces.NearBySearch.QueryAsync(new PlacesNearBySearchRequest
            {
                Key = GoogleApiKey,
                Name = name,
                Location = new GoogleApi.Entities.Places.Search.NearBy.Request.Location(latitude, longitude),
                Radius = 1000
            });

            // If we did not get a good response, or the list of results are empty then get out of here
            if (!searchResponse.Status.HasValue || searchResponse.Status.Value != Status.Ok || !searchResponse.Results.Any())
                return new BadRequestResult();

            // Get the first result
            var nearbyResult = searchResponse.Results.FirstOrDefault();
            string placeId = nearbyResult.PlaceId;
            string photoReference = nearbyResult.Photos?.FirstOrDefault()?.PhotoReference;
            string photoCredit = nearbyResult.Photos?.FirstOrDefault()?.HtmlAttributions.FirstOrDefault();

            // Execute the details request
            var detailsResonse = await GooglePlaces.Details.QueryAsync(new PlacesDetailsRequest
            {
                Key = GoogleApiKey,
                PlaceId = placeId
            });

            // If we did not get a good response then get out of here
            if (!detailsResonse.Status.HasValue || detailsResonse.Status.Value != Status.Ok)
                return new BadRequestResult();

            // Set the details
            var detailsResult = detailsResonse.Result;
            propertyDetail.FormattedAddress = detailsResult.FormattedAddress;
            propertyDetail.PhoneNumber = detailsResult.InternationalPhoneNumber;
            propertyDetail.Website = detailsResult.Website;

            if (photoReference != null)
            {
                // Execute the photo request
                var photosResponse = await GooglePlaces.Photos.QueryAsync(new PlacesPhotosRequest
                {
                    Key = GoogleApiKey,
                    PhotoReference = photoReference,
                    MaxWidth = 400
                });

                if (photosResponse.Buffer != null)
                {
                    propertyDetail.Photo = Convert.ToBase64String(photosResponse.Buffer);
                    propertyDetail.PhotoCredit = photoCredit;
                }
            }

            return new JsonResult(propertyDetail);
        }
    }
}