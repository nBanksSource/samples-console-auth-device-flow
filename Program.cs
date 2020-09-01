﻿using Clients;
using IdentityModel;
using IdentityModel.Client;
using Newtonsoft.Json.Linq;
using System;
using System.Diagnostics;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
namespace console_auth_device_flow
{
    class Program
    {
        static IDiscoveryCache _cache = new DiscoveryCache(ConstantsSandbox.Authority);
        static DiscoveryDocumentResponse disco;


        static async Task Main(string[] args)
        {
            Console.Title = "Console Device Flow";
            //Request Device Authorization
            var authorizeResponse = await RequestAuthorizationAsync();

            //Request Token
            var tokenResponse = await RequestTokenAsync(authorizeResponse);
            tokenResponse.Show();

            //Refresh Token
            Console.WriteLine($"\nPress enter to refresh token");
            Console.ReadLine();
            tokenResponse = await RefreshTokenAsync(tokenResponse.RefreshToken);
            tokenResponse.Show();

            //Call API with TOKEN
            Console.WriteLine($"\nPress enter to call PUBLIC-API");
            Console.ReadLine();
            await CallServiceAsync(tokenResponse.AccessToken);
        }

        static async Task<DeviceAuthorizationResponse> RequestAuthorizationAsync()
        {


            var client = new HttpClient();
            disco = await client.GetDiscoveryDocumentAsync(new DiscoveryDocumentRequest
            {
                Address = ConstantsSandbox.Authority
            });

            var response = await client.RequestDeviceAuthorizationAsync(new DeviceAuthorizationRequest
            {
                Address = disco.DeviceAuthorizationEndpoint,
                ClientId = ConstantsSandbox.SampleClientId,
                ClientSecret = ConstantsSandbox.SampleClientSecret
            });

            if (response.IsError) throw new Exception(response.Error);

            Console.WriteLine($"user code   : {response.UserCode}");
            Console.WriteLine($"device code : {response.DeviceCode}");
            Console.WriteLine($"URL         : {response.VerificationUri}");
            Console.WriteLine($"Complete URL: {response.VerificationUriComplete}");

            Console.WriteLine($"\nPress enter to launch browser ({response.VerificationUri})");
            Console.ReadLine();

            Process.Start(new ProcessStartInfo(response.VerificationUriComplete) { UseShellExecute = true });
            return response;
        }

        private static async Task<TokenResponse> RequestTokenAsync(DeviceAuthorizationResponse authorizeResponse)
        {
            
            var client = new HttpClient();

            while (true)
            {
                var response = await client.RequestDeviceTokenAsync(new DeviceTokenRequest
                {
                    Address = disco.TokenEndpoint,
                    ClientId = ConstantsSandbox.SampleClientId,
                    ClientSecret = ConstantsSandbox.SampleClientSecret,
                    DeviceCode = authorizeResponse.DeviceCode
                });

                if (response.IsError)
                {
                    if (response.Error == OidcConstants.TokenErrors.AuthorizationPending || response.Error == OidcConstants.TokenErrors.SlowDown)
                    {
                        Console.WriteLine($"{response.Error}...waiting.");
                        Thread.Sleep(authorizeResponse.Interval * 1000);
                    }
                    else
                    {
                        throw new Exception(response.Error);
                    }
                }
                else
                {
                    return response;
                }
            }
        }

        private static async Task<TokenResponse> RefreshTokenAsync(string refreshToken)
        {
            Console.WriteLine("Using refresh token: {0}", refreshToken);
            
            var baseAddress = ConstantsSandbox.SampleApi;

            var client = new HttpClient
            {
                BaseAddress = new Uri(baseAddress),
            };

            var response = await client.RequestRefreshTokenAsync(new RefreshTokenRequest
            {
                Address = disco.TokenEndpoint,
                ClientId = ConstantsSandbox.SampleClientId,
                ClientSecret = ConstantsSandbox.SampleClientSecret,
                RefreshToken = refreshToken
            });

            if (response.IsError) throw new Exception(response.Error);
            return response;
        }

        static async Task CallServiceAsync(string token)
        {
            var baseAddress = ConstantsSandbox.SampleApi;

            var client = new HttpClient
            {
                BaseAddress = new Uri(baseAddress),
            };

            client.SetBearerToken(token);
            var response = await client.GetStringAsync("api/v1/Organizations");

            "\n\nService claims:".ConsoleGreen();
            Console.WriteLine(JObject.Parse(response));
        }
    }
}
