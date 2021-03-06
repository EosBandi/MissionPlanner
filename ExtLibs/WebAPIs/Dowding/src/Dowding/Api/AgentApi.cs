/* 
 * Dowding HTTP REST API
 *
 * The Dowding HTTP REST API allows you to add and retrieve contact data from Dowding as well as perform other peripheral functions.
 *
 * OpenAPI spec version: 1.0.0
 * 
 * Generated by: https://github.com/swagger-api/swagger-codegen.git
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *      http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using RestSharp;
using Dowding.Client;
using Dowding.Model;

namespace Dowding.Api
{
    /// <summary>
    /// Represents a collection of functions to interact with the API endpoints
    /// </summary>
    public interface IAgentApi : IApiAccessor
    {
        #region Synchronous Operations
        /// <summary>
        /// 
        /// </summary>
        /// <remarks>
        /// 
        /// </remarks>
        /// <exception cref="Dowding.Client.ApiException">Thrown when fails to make API call</exception>
        /// <param name="type"> (optional)</param>
        /// <returns>List&lt;Agent&gt;</returns>
        List<Agent> AgentGet (string type = null);

        /// <summary>
        /// 
        /// </summary>
        /// <remarks>
        /// 
        /// </remarks>
        /// <exception cref="Dowding.Client.ApiException">Thrown when fails to make API call</exception>
        /// <param name="type"> (optional)</param>
        /// <returns>ApiResponse of List&lt;Agent&gt;</returns>
        ApiResponse<List<Agent>> AgentGetWithHttpInfo (string type = null);
        /// <summary>
        /// 
        /// </summary>
        /// <remarks>
        /// 
        /// </remarks>
        /// <exception cref="Dowding.Client.ApiException">Thrown when fails to make API call</exception>
        /// <param name="agentUpdateDto"></param>
        /// <returns>AgentLoginDto</returns>
        AgentLoginDto AgentPost (AgentUpdateDto agentUpdateDto);

        /// <summary>
        /// 
        /// </summary>
        /// <remarks>
        /// 
        /// </remarks>
        /// <exception cref="Dowding.Client.ApiException">Thrown when fails to make API call</exception>
        /// <param name="agentUpdateDto"></param>
        /// <returns>ApiResponse of AgentLoginDto</returns>
        ApiResponse<AgentLoginDto> AgentPostWithHttpInfo (AgentUpdateDto agentUpdateDto);
        /// <summary>
        /// 
        /// </summary>
        /// <remarks>
        /// 
        /// </remarks>
        /// <exception cref="Dowding.Client.ApiException">Thrown when fails to make API call</exception>
        /// <param name="id"></param>
        /// <returns>AgentStatusGetDto</returns>
        AgentStatusGetDto AgentStatusIdGet (string id);

        /// <summary>
        /// 
        /// </summary>
        /// <remarks>
        /// 
        /// </remarks>
        /// <exception cref="Dowding.Client.ApiException">Thrown when fails to make API call</exception>
        /// <param name="id"></param>
        /// <returns>ApiResponse of AgentStatusGetDto</returns>
        ApiResponse<AgentStatusGetDto> AgentStatusIdGetWithHttpInfo (string id);
        #endregion Synchronous Operations
        #region Asynchronous Operations
        /// <summary>
        /// 
        /// </summary>
        /// <remarks>
        /// 
        /// </remarks>
        /// <exception cref="Dowding.Client.ApiException">Thrown when fails to make API call</exception>
        /// <param name="type"> (optional)</param>
        /// <returns>Task of List&lt;Agent&gt;</returns>
        System.Threading.Tasks.Task<List<Agent>> AgentGetAsync (string type = null);

        /// <summary>
        /// 
        /// </summary>
        /// <remarks>
        /// 
        /// </remarks>
        /// <exception cref="Dowding.Client.ApiException">Thrown when fails to make API call</exception>
        /// <param name="type"> (optional)</param>
        /// <returns>Task of ApiResponse (List&lt;Agent&gt;)</returns>
        System.Threading.Tasks.Task<ApiResponse<List<Agent>>> AgentGetAsyncWithHttpInfo (string type = null);
        /// <summary>
        /// 
        /// </summary>
        /// <remarks>
        /// 
        /// </remarks>
        /// <exception cref="Dowding.Client.ApiException">Thrown when fails to make API call</exception>
        /// <param name="agentUpdateDto"></param>
        /// <returns>Task of AgentLoginDto</returns>
        System.Threading.Tasks.Task<AgentLoginDto> AgentPostAsync (AgentUpdateDto agentUpdateDto);

        /// <summary>
        /// 
        /// </summary>
        /// <remarks>
        /// 
        /// </remarks>
        /// <exception cref="Dowding.Client.ApiException">Thrown when fails to make API call</exception>
        /// <param name="agentUpdateDto"></param>
        /// <returns>Task of ApiResponse (AgentLoginDto)</returns>
        System.Threading.Tasks.Task<ApiResponse<AgentLoginDto>> AgentPostAsyncWithHttpInfo (AgentUpdateDto agentUpdateDto);
        /// <summary>
        /// 
        /// </summary>
        /// <remarks>
        /// 
        /// </remarks>
        /// <exception cref="Dowding.Client.ApiException">Thrown when fails to make API call</exception>
        /// <param name="id"></param>
        /// <returns>Task of AgentStatusGetDto</returns>
        System.Threading.Tasks.Task<AgentStatusGetDto> AgentStatusIdGetAsync (string id);

        /// <summary>
        /// 
        /// </summary>
        /// <remarks>
        /// 
        /// </remarks>
        /// <exception cref="Dowding.Client.ApiException">Thrown when fails to make API call</exception>
        /// <param name="id"></param>
        /// <returns>Task of ApiResponse (AgentStatusGetDto)</returns>
        System.Threading.Tasks.Task<ApiResponse<AgentStatusGetDto>> AgentStatusIdGetAsyncWithHttpInfo (string id);
        #endregion Asynchronous Operations
    }

    /// <summary>
    /// Represents a collection of functions to interact with the API endpoints
    /// </summary>
    public partial class AgentApi : IAgentApi
    {
        private Dowding.Client.ExceptionFactory _exceptionFactory = (name, response) => null;

        /// <summary>
        /// Initializes a new instance of the <see cref="AgentApi"/> class.
        /// </summary>
        /// <returns></returns>
        public AgentApi(String basePath)
        {
            this.Configuration = new Configuration(new ApiClient(basePath));

            ExceptionFactory = Dowding.Client.Configuration.DefaultExceptionFactory;

            // ensure API client has configuration ready
            if (Configuration.ApiClient.Configuration == null)
            {
                this.Configuration.ApiClient.Configuration = this.Configuration;
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="AgentApi"/> class
        /// using Configuration object
        /// </summary>
        /// <param name="configuration">An instance of Configuration</param>
        /// <returns></returns>
        public AgentApi(Configuration configuration = null)
        {
            if (configuration == null) // use the default one in Configuration
                this.Configuration = Configuration.Default;
            else
                this.Configuration = configuration;

            ExceptionFactory = Dowding.Client.Configuration.DefaultExceptionFactory;

            // ensure API client has configuration ready
            if (Configuration.ApiClient.Configuration == null)
            {
                this.Configuration.ApiClient.Configuration = this.Configuration;
            }
        }

        /// <summary>
        /// Gets the base path of the API client.
        /// </summary>
        /// <value>The base path</value>
        public String GetBasePath()
        {
            return this.Configuration.ApiClient.RestClient.BaseUrl.ToString();
        }

        /// <summary>
        /// Sets the base path of the API client.
        /// </summary>
        /// <value>The base path</value>
        [Obsolete("SetBasePath is deprecated, please do 'Configuration.ApiClient = new ApiClient(\"http://new-path\")' instead.")]
        public void SetBasePath(String basePath)
        {
            // do nothing
        }

        /// <summary>
        /// Gets or sets the configuration object
        /// </summary>
        /// <value>An instance of the Configuration</value>
        public Configuration Configuration {get; set;}

        /// <summary>
        /// Provides a factory method hook for the creation of exceptions.
        /// </summary>
        public Dowding.Client.ExceptionFactory ExceptionFactory
        {
            get
            {
                if (_exceptionFactory != null && _exceptionFactory.GetInvocationList().Length > 1)
                {
                    throw new InvalidOperationException("Multicast delegate for ExceptionFactory is unsupported.");
                }
                return _exceptionFactory;
            }
            set { _exceptionFactory = value; }
        }

        /// <summary>
        /// Gets the default header.
        /// </summary>
        /// <returns>Dictionary of HTTP header</returns>
        [Obsolete("DefaultHeader is deprecated, please use Configuration.DefaultHeader instead.")]
        public Dictionary<String, String> DefaultHeader()
        {
            return this.Configuration.DefaultHeader;
        }

        /// <summary>
        /// Add default header.
        /// </summary>
        /// <param name="key">Header field name.</param>
        /// <param name="value">Header field value.</param>
        /// <returns></returns>
        [Obsolete("AddDefaultHeader is deprecated, please use Configuration.AddDefaultHeader instead.")]
        public void AddDefaultHeader(string key, string value)
        {
            this.Configuration.AddDefaultHeader(key, value);
        }

        /// <summary>
        ///  
        /// </summary>
        /// <exception cref="Dowding.Client.ApiException">Thrown when fails to make API call</exception>
        /// <param name="type"> (optional)</param>
        /// <returns>List&lt;Agent&gt;</returns>
        public List<Agent> AgentGet (string type = null)
        {
             ApiResponse<List<Agent>> localVarResponse = AgentGetWithHttpInfo(type);
             return localVarResponse.Data;
        }

        /// <summary>
        ///  
        /// </summary>
        /// <exception cref="Dowding.Client.ApiException">Thrown when fails to make API call</exception>
        /// <param name="type"> (optional)</param>
        /// <returns>ApiResponse of List&lt;Agent&gt;</returns>
        public ApiResponse< List<Agent> > AgentGetWithHttpInfo (string type = null)
        {

            var localVarPath = "/agent";
            var localVarPathParams = new Dictionary<String, String>();
            var localVarQueryParams = new Dictionary<String, String>();
            var localVarHeaderParams = new Dictionary<String, String>(Configuration.DefaultHeader);
            var localVarFormParams = new Dictionary<String, String>();
            var localVarFileParams = new Dictionary<String, FileParameter>();
            Object localVarPostBody = null;

            // to determine the Content-Type header
            String[] localVarHttpContentTypes = new String[] {
                "application/json"
            };
            String localVarHttpContentType = Configuration.ApiClient.SelectHeaderContentType(localVarHttpContentTypes);

            // to determine the Accept header
            String[] localVarHttpHeaderAccepts = new String[] {
                "application/json"
            };
            String localVarHttpHeaderAccept = Configuration.ApiClient.SelectHeaderAccept(localVarHttpHeaderAccepts);
            if (localVarHttpHeaderAccept != null)
                localVarHeaderParams.Add("Accept", localVarHttpHeaderAccept);

            // set "format" to json by default
            // e.g. /pet/{petId}.{format} becomes /pet/{petId}.json
            localVarPathParams.Add("format", "json");
            if (type != null) localVarQueryParams.Add("type", Configuration.ApiClient.ParameterToString(type)); // query parameter

            // authentication (bearer) required
            if (!String.IsNullOrEmpty(Configuration.GetApiKeyWithPrefix("Authorization")))
            {
                localVarHeaderParams["Authorization"] = Configuration.GetApiKeyWithPrefix("Authorization");
            }


            // make the HTTP request
            IRestResponse localVarResponse = (IRestResponse) Configuration.ApiClient.CallApi(localVarPath,
                Method.GET, localVarQueryParams, localVarPostBody, localVarHeaderParams, localVarFormParams, localVarFileParams,
                localVarPathParams, localVarHttpContentType);

            int localVarStatusCode = (int) localVarResponse.StatusCode;

            if (ExceptionFactory != null)
            {
                Exception exception = ExceptionFactory("AgentGet", localVarResponse);
                if (exception != null) throw exception;
            }

            return new ApiResponse<List<Agent>>(localVarStatusCode,
                localVarResponse.Headers.ToDictionary(x => x.Name, x => x.Value.ToString()),
                (List<Agent>) Configuration.ApiClient.Deserialize(localVarResponse, typeof(List<Agent>)));
            
        }

        /// <summary>
        ///  
        /// </summary>
        /// <exception cref="Dowding.Client.ApiException">Thrown when fails to make API call</exception>
        /// <param name="type"> (optional)</param>
        /// <returns>Task of List&lt;Agent&gt;</returns>
        public async System.Threading.Tasks.Task<List<Agent>> AgentGetAsync (string type = null)
        {
             ApiResponse<List<Agent>> localVarResponse = await AgentGetAsyncWithHttpInfo(type);
             return localVarResponse.Data;

        }

        /// <summary>
        ///  
        /// </summary>
        /// <exception cref="Dowding.Client.ApiException">Thrown when fails to make API call</exception>
        /// <param name="type"> (optional)</param>
        /// <returns>Task of ApiResponse (List&lt;Agent&gt;)</returns>
        public async System.Threading.Tasks.Task<ApiResponse<List<Agent>>> AgentGetAsyncWithHttpInfo (string type = null)
        {

            var localVarPath = "/agent";
            var localVarPathParams = new Dictionary<String, String>();
            var localVarQueryParams = new Dictionary<String, String>();
            var localVarHeaderParams = new Dictionary<String, String>(Configuration.DefaultHeader);
            var localVarFormParams = new Dictionary<String, String>();
            var localVarFileParams = new Dictionary<String, FileParameter>();
            Object localVarPostBody = null;

            // to determine the Content-Type header
            String[] localVarHttpContentTypes = new String[] {
                "application/json"
            };
            String localVarHttpContentType = Configuration.ApiClient.SelectHeaderContentType(localVarHttpContentTypes);

            // to determine the Accept header
            String[] localVarHttpHeaderAccepts = new String[] {
                "application/json"
            };
            String localVarHttpHeaderAccept = Configuration.ApiClient.SelectHeaderAccept(localVarHttpHeaderAccepts);
            if (localVarHttpHeaderAccept != null)
                localVarHeaderParams.Add("Accept", localVarHttpHeaderAccept);

            // set "format" to json by default
            // e.g. /pet/{petId}.{format} becomes /pet/{petId}.json
            localVarPathParams.Add("format", "json");
            if (type != null) localVarQueryParams.Add("type", Configuration.ApiClient.ParameterToString(type)); // query parameter

            // authentication (bearer) required
            if (!String.IsNullOrEmpty(Configuration.GetApiKeyWithPrefix("Authorization")))
            {
                localVarHeaderParams["Authorization"] = Configuration.GetApiKeyWithPrefix("Authorization");
            }

            // make the HTTP request
            IRestResponse localVarResponse = (IRestResponse) await Configuration.ApiClient.CallApiAsync(localVarPath,
                Method.GET, localVarQueryParams, localVarPostBody, localVarHeaderParams, localVarFormParams, localVarFileParams,
                localVarPathParams, localVarHttpContentType);

            int localVarStatusCode = (int) localVarResponse.StatusCode;

            if (ExceptionFactory != null)
            {
                Exception exception = ExceptionFactory("AgentGet", localVarResponse);
                if (exception != null) throw exception;
            }

            return new ApiResponse<List<Agent>>(localVarStatusCode,
                localVarResponse.Headers.ToDictionary(x => x.Name, x => x.Value.ToString()),
                (List<Agent>) Configuration.ApiClient.Deserialize(localVarResponse, typeof(List<Agent>)));
            
        }

        /// <summary>
        ///  
        /// </summary>
        /// <exception cref="Dowding.Client.ApiException">Thrown when fails to make API call</exception>
        /// <param name="agentUpdateDto"></param>
        /// <returns>AgentLoginDto</returns>
        public AgentLoginDto AgentPost (AgentUpdateDto agentUpdateDto)
        {
             ApiResponse<AgentLoginDto> localVarResponse = AgentPostWithHttpInfo(agentUpdateDto);
             return localVarResponse.Data;
        }

        /// <summary>
        ///  
        /// </summary>
        /// <exception cref="Dowding.Client.ApiException">Thrown when fails to make API call</exception>
        /// <param name="agentUpdateDto"></param>
        /// <returns>ApiResponse of AgentLoginDto</returns>
        public ApiResponse< AgentLoginDto > AgentPostWithHttpInfo (AgentUpdateDto agentUpdateDto)
        {
            // verify the required parameter 'agentUpdateDto' is set
            if (agentUpdateDto == null)
                throw new ApiException(400, "Missing required parameter 'agentUpdateDto' when calling AgentApi->AgentPost");

            var localVarPath = "/agent";
            var localVarPathParams = new Dictionary<String, String>();
            var localVarQueryParams = new Dictionary<String, String>();
            var localVarHeaderParams = new Dictionary<String, String>(Configuration.DefaultHeader);
            var localVarFormParams = new Dictionary<String, String>();
            var localVarFileParams = new Dictionary<String, FileParameter>();
            Object localVarPostBody = null;

            // to determine the Content-Type header
            String[] localVarHttpContentTypes = new String[] {
                "application/json"
            };
            String localVarHttpContentType = Configuration.ApiClient.SelectHeaderContentType(localVarHttpContentTypes);

            // to determine the Accept header
            String[] localVarHttpHeaderAccepts = new String[] {
                "application/json"
            };
            String localVarHttpHeaderAccept = Configuration.ApiClient.SelectHeaderAccept(localVarHttpHeaderAccepts);
            if (localVarHttpHeaderAccept != null)
                localVarHeaderParams.Add("Accept", localVarHttpHeaderAccept);

            // set "format" to json by default
            // e.g. /pet/{petId}.{format} becomes /pet/{petId}.json
            localVarPathParams.Add("format", "json");
            if (agentUpdateDto != null && agentUpdateDto.GetType() != typeof(byte[]))
            {
                localVarPostBody = Configuration.ApiClient.Serialize(agentUpdateDto); // http body (model) parameter
            }
            else
            {
                localVarPostBody = agentUpdateDto; // byte array
            }

            // authentication (bearer) required
            if (!String.IsNullOrEmpty(Configuration.GetApiKeyWithPrefix("Authorization")))
            {
                localVarHeaderParams["Authorization"] = Configuration.GetApiKeyWithPrefix("Authorization");
            }


            // make the HTTP request
            IRestResponse localVarResponse = (IRestResponse) Configuration.ApiClient.CallApi(localVarPath,
                Method.POST, localVarQueryParams, localVarPostBody, localVarHeaderParams, localVarFormParams, localVarFileParams,
                localVarPathParams, localVarHttpContentType);

            int localVarStatusCode = (int) localVarResponse.StatusCode;

            if (ExceptionFactory != null)
            {
                Exception exception = ExceptionFactory("AgentPost", localVarResponse);
                if (exception != null) throw exception;
            }

            return new ApiResponse<AgentLoginDto>(localVarStatusCode,
                localVarResponse.Headers.ToDictionary(x => x.Name, x => x.Value.ToString()),
                (AgentLoginDto) Configuration.ApiClient.Deserialize(localVarResponse, typeof(AgentLoginDto)));
            
        }

        /// <summary>
        ///  
        /// </summary>
        /// <exception cref="Dowding.Client.ApiException">Thrown when fails to make API call</exception>
        /// <param name="agentUpdateDto"></param>
        /// <returns>Task of AgentLoginDto</returns>
        public async System.Threading.Tasks.Task<AgentLoginDto> AgentPostAsync (AgentUpdateDto agentUpdateDto)
        {
             ApiResponse<AgentLoginDto> localVarResponse = await AgentPostAsyncWithHttpInfo(agentUpdateDto);
             return localVarResponse.Data;

        }

        /// <summary>
        ///  
        /// </summary>
        /// <exception cref="Dowding.Client.ApiException">Thrown when fails to make API call</exception>
        /// <param name="agentUpdateDto"></param>
        /// <returns>Task of ApiResponse (AgentLoginDto)</returns>
        public async System.Threading.Tasks.Task<ApiResponse<AgentLoginDto>> AgentPostAsyncWithHttpInfo (AgentUpdateDto agentUpdateDto)
        {
            // verify the required parameter 'agentUpdateDto' is set
            if (agentUpdateDto == null)
                throw new ApiException(400, "Missing required parameter 'agentUpdateDto' when calling AgentApi->AgentPost");

            var localVarPath = "/agent";
            var localVarPathParams = new Dictionary<String, String>();
            var localVarQueryParams = new Dictionary<String, String>();
            var localVarHeaderParams = new Dictionary<String, String>(Configuration.DefaultHeader);
            var localVarFormParams = new Dictionary<String, String>();
            var localVarFileParams = new Dictionary<String, FileParameter>();
            Object localVarPostBody = null;

            // to determine the Content-Type header
            String[] localVarHttpContentTypes = new String[] {
                "application/json"
            };
            String localVarHttpContentType = Configuration.ApiClient.SelectHeaderContentType(localVarHttpContentTypes);

            // to determine the Accept header
            String[] localVarHttpHeaderAccepts = new String[] {
                "application/json"
            };
            String localVarHttpHeaderAccept = Configuration.ApiClient.SelectHeaderAccept(localVarHttpHeaderAccepts);
            if (localVarHttpHeaderAccept != null)
                localVarHeaderParams.Add("Accept", localVarHttpHeaderAccept);

            // set "format" to json by default
            // e.g. /pet/{petId}.{format} becomes /pet/{petId}.json
            localVarPathParams.Add("format", "json");
            if (agentUpdateDto != null && agentUpdateDto.GetType() != typeof(byte[]))
            {
                localVarPostBody = Configuration.ApiClient.Serialize(agentUpdateDto); // http body (model) parameter
            }
            else
            {
                localVarPostBody = agentUpdateDto; // byte array
            }

            // authentication (bearer) required
            if (!String.IsNullOrEmpty(Configuration.GetApiKeyWithPrefix("Authorization")))
            {
                localVarHeaderParams["Authorization"] = Configuration.GetApiKeyWithPrefix("Authorization");
            }

            // make the HTTP request
            IRestResponse localVarResponse = (IRestResponse) await Configuration.ApiClient.CallApiAsync(localVarPath,
                Method.POST, localVarQueryParams, localVarPostBody, localVarHeaderParams, localVarFormParams, localVarFileParams,
                localVarPathParams, localVarHttpContentType);

            int localVarStatusCode = (int) localVarResponse.StatusCode;

            if (ExceptionFactory != null)
            {
                Exception exception = ExceptionFactory("AgentPost", localVarResponse);
                if (exception != null) throw exception;
            }

            return new ApiResponse<AgentLoginDto>(localVarStatusCode,
                localVarResponse.Headers.ToDictionary(x => x.Name, x => x.Value.ToString()),
                (AgentLoginDto) Configuration.ApiClient.Deserialize(localVarResponse, typeof(AgentLoginDto)));
            
        }

        /// <summary>
        ///  
        /// </summary>
        /// <exception cref="Dowding.Client.ApiException">Thrown when fails to make API call</exception>
        /// <param name="id"></param>
        /// <returns>AgentStatusGetDto</returns>
        public AgentStatusGetDto AgentStatusIdGet (string id)
        {
             ApiResponse<AgentStatusGetDto> localVarResponse = AgentStatusIdGetWithHttpInfo(id);
             return localVarResponse.Data;
        }

        /// <summary>
        ///  
        /// </summary>
        /// <exception cref="Dowding.Client.ApiException">Thrown when fails to make API call</exception>
        /// <param name="id"></param>
        /// <returns>ApiResponse of AgentStatusGetDto</returns>
        public ApiResponse< AgentStatusGetDto > AgentStatusIdGetWithHttpInfo (string id)
        {
            // verify the required parameter 'id' is set
            if (id == null)
                throw new ApiException(400, "Missing required parameter 'id' when calling AgentApi->AgentStatusIdGet");

            var localVarPath = "/agent/status/{id}";
            var localVarPathParams = new Dictionary<String, String>();
            var localVarQueryParams = new Dictionary<String, String>();
            var localVarHeaderParams = new Dictionary<String, String>(Configuration.DefaultHeader);
            var localVarFormParams = new Dictionary<String, String>();
            var localVarFileParams = new Dictionary<String, FileParameter>();
            Object localVarPostBody = null;

            // to determine the Content-Type header
            String[] localVarHttpContentTypes = new String[] {
                "application/json"
            };
            String localVarHttpContentType = Configuration.ApiClient.SelectHeaderContentType(localVarHttpContentTypes);

            // to determine the Accept header
            String[] localVarHttpHeaderAccepts = new String[] {
                "application/json"
            };
            String localVarHttpHeaderAccept = Configuration.ApiClient.SelectHeaderAccept(localVarHttpHeaderAccepts);
            if (localVarHttpHeaderAccept != null)
                localVarHeaderParams.Add("Accept", localVarHttpHeaderAccept);

            // set "format" to json by default
            // e.g. /pet/{petId}.{format} becomes /pet/{petId}.json
            localVarPathParams.Add("format", "json");
            if (id != null) localVarPathParams.Add("id", Configuration.ApiClient.ParameterToString(id)); // path parameter

            // authentication (bearer) required
            if (!String.IsNullOrEmpty(Configuration.GetApiKeyWithPrefix("Authorization")))
            {
                localVarHeaderParams["Authorization"] = Configuration.GetApiKeyWithPrefix("Authorization");
            }


            // make the HTTP request
            IRestResponse localVarResponse = (IRestResponse) Configuration.ApiClient.CallApi(localVarPath,
                Method.GET, localVarQueryParams, localVarPostBody, localVarHeaderParams, localVarFormParams, localVarFileParams,
                localVarPathParams, localVarHttpContentType);

            int localVarStatusCode = (int) localVarResponse.StatusCode;

            if (ExceptionFactory != null)
            {
                Exception exception = ExceptionFactory("AgentStatusIdGet", localVarResponse);
                if (exception != null) throw exception;
            }

            return new ApiResponse<AgentStatusGetDto>(localVarStatusCode,
                localVarResponse.Headers.ToDictionary(x => x.Name, x => x.Value.ToString()),
                (AgentStatusGetDto) Configuration.ApiClient.Deserialize(localVarResponse, typeof(AgentStatusGetDto)));
            
        }

        /// <summary>
        ///  
        /// </summary>
        /// <exception cref="Dowding.Client.ApiException">Thrown when fails to make API call</exception>
        /// <param name="id"></param>
        /// <returns>Task of AgentStatusGetDto</returns>
        public async System.Threading.Tasks.Task<AgentStatusGetDto> AgentStatusIdGetAsync (string id)
        {
             ApiResponse<AgentStatusGetDto> localVarResponse = await AgentStatusIdGetAsyncWithHttpInfo(id);
             return localVarResponse.Data;

        }

        /// <summary>
        ///  
        /// </summary>
        /// <exception cref="Dowding.Client.ApiException">Thrown when fails to make API call</exception>
        /// <param name="id"></param>
        /// <returns>Task of ApiResponse (AgentStatusGetDto)</returns>
        public async System.Threading.Tasks.Task<ApiResponse<AgentStatusGetDto>> AgentStatusIdGetAsyncWithHttpInfo (string id)
        {
            // verify the required parameter 'id' is set
            if (id == null)
                throw new ApiException(400, "Missing required parameter 'id' when calling AgentApi->AgentStatusIdGet");

            var localVarPath = "/agent/status/{id}";
            var localVarPathParams = new Dictionary<String, String>();
            var localVarQueryParams = new Dictionary<String, String>();
            var localVarHeaderParams = new Dictionary<String, String>(Configuration.DefaultHeader);
            var localVarFormParams = new Dictionary<String, String>();
            var localVarFileParams = new Dictionary<String, FileParameter>();
            Object localVarPostBody = null;

            // to determine the Content-Type header
            String[] localVarHttpContentTypes = new String[] {
                "application/json"
            };
            String localVarHttpContentType = Configuration.ApiClient.SelectHeaderContentType(localVarHttpContentTypes);

            // to determine the Accept header
            String[] localVarHttpHeaderAccepts = new String[] {
                "application/json"
            };
            String localVarHttpHeaderAccept = Configuration.ApiClient.SelectHeaderAccept(localVarHttpHeaderAccepts);
            if (localVarHttpHeaderAccept != null)
                localVarHeaderParams.Add("Accept", localVarHttpHeaderAccept);

            // set "format" to json by default
            // e.g. /pet/{petId}.{format} becomes /pet/{petId}.json
            localVarPathParams.Add("format", "json");
            if (id != null) localVarPathParams.Add("id", Configuration.ApiClient.ParameterToString(id)); // path parameter

            // authentication (bearer) required
            if (!String.IsNullOrEmpty(Configuration.GetApiKeyWithPrefix("Authorization")))
            {
                localVarHeaderParams["Authorization"] = Configuration.GetApiKeyWithPrefix("Authorization");
            }

            // make the HTTP request
            IRestResponse localVarResponse = (IRestResponse) await Configuration.ApiClient.CallApiAsync(localVarPath,
                Method.GET, localVarQueryParams, localVarPostBody, localVarHeaderParams, localVarFormParams, localVarFileParams,
                localVarPathParams, localVarHttpContentType);

            int localVarStatusCode = (int) localVarResponse.StatusCode;

            if (ExceptionFactory != null)
            {
                Exception exception = ExceptionFactory("AgentStatusIdGet", localVarResponse);
                if (exception != null) throw exception;
            }

            return new ApiResponse<AgentStatusGetDto>(localVarStatusCode,
                localVarResponse.Headers.ToDictionary(x => x.Name, x => x.Value.ToString()),
                (AgentStatusGetDto) Configuration.ApiClient.Deserialize(localVarResponse, typeof(AgentStatusGetDto)));
            
        }

    }
}
