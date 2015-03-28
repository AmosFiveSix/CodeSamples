// Originally based on: Ajax framework for Internet Explorer (6.0, ...) and Firefox (1.0, ...) Copyright (c) by Matthias Hertel, http://www.mathertel.de

// This is the AJAX proxy code used to call .NET ASMX SOAP web services. The basics were here when I started working with the company but 
// I've significantly modified it since then. Most recently preparing to switch from ASMX to WCF. 

// The ajax module and SoapRequest class are entirely my own. That's the part that give us async web calls. Previouly we'd only used
// synchronous calls (sigh). We were also afraid of 3rd party libraries so jQuery was not an option at the time.

// A key part of this system that's not here is the generating of the JavaScript proxy code that calls into this code. It's created by 
// some server side C# using the web service's WSDL. It looks basically like the first chunk below.

// Sample proxies code generated from WSDL

proxies.FacilityGroup =
{
    url: "http://build7:8100/DataArk4/Services/FacilityGroup.asmx",
    ns: "http://www.mediquant.com/DataArk4/"
};

proxies.FacilityGroup.Delete = function()
{
    return (proxies.callSoap(arguments));
};

proxies.FacilityGroup.Delete.fname = "Delete";
proxies.FacilityGroup.Delete.service = proxies.FacilityGroup;
proxies.FacilityGroup.Delete.action = "\"http://www.mediquant.com/DataArk4/Delete\"";
proxies.FacilityGroup.Delete.params = ["facilityGroupId"];
proxies.FacilityGroup.Delete.rtype = [];

// The behind the scenes stuff that makes the proxies calls work.

var soap = (function()
{
	/* Return the Public API */

	return (
	{
		buildRequest: buildRequest,

		parseResponse: parseResponse
	});

	/* Public API Methods */

	function buildRequest(method, ns, types, values)
	{
		var parameters, count, index, info, name, type, arrayNS, value, xml;

		parameters = '';

		if(types && values)
		{
			count = (values.length < types.length) ? values.length : types.length;
		}
		else
		{
			count = 0;
		}

		for(index = 0; index < count; index++)
		{
			// types is an array of strings in the format "name:type". values is an array of the actual values for each parameter.
			// If the type is an array it's in the format "name:type:namespace". We have to work a little harder to extract the
			// array namespace because namespaces can have colons in them, like http://schemas.microsoft.com/2003/10/Serialization/Arrays
			// So we do a substring on the full "name:type:namespace" using the length of the name and type substrings.

			info = types[index].split(':');

			name = info[0];
			type = info[1] || '';

			arrayNS = types[index].substr(name.length + 1 + type.length + 1);	// See notes above. We add the ones to skip past the colons.

			value = values[index];

			parameters += buildParameter(method, name, type, arrayNS, value);
		}

		xml =  "<?xml version='1.0' encoding='utf-8'?>";
		xml += "<soap:Envelope xmlns:soap='http://schemas.xmlsoap.org/soap/envelope/'>";
		xml += "<soap:Body>";
		xml += "<" + method + " xmlns='" + ns + "'>";
		xml += parameters;
		xml += "</" + method + ">";
		xml += "</soap:Body>";
		xml += "</soap:Envelope>";

		return xml;
	}

	function parseResponse(document, response, statusCode, statusText, proxy, options)
	{
		var ok, xml, node, detail;

		ok = (statusCode >= 200 && statusCode <= 299) || (statusCode === 304);

		if(!document || $xml.hasParserException(document))
		{
			// The SOAP that was sent was not valid XML or our client-side XML parser cannot parse it.

			if(ok)
			{
				// The server said things were successful but sent us garbage. We just fail in that case since we don't need to extract any error information from the response.

				return { exception: createInvalidException(response, statusCode) };
			}
			else
			{
				// The server said things failed but sent us garbage. We try to clean up the garbage so we can extract error information from the SOAP XML.

				xml = $xml.clean(response);

				document = $xml.deserialize(xml);

				if($xml.hasParserException(document))
				{
					// We tried cleaning up the XML but it's still invalid. We create an exception out of the status code.

					return { exception: createStatusException(response, statusCode, statusText) };
				}
			}
		}

		// At this point we know we have valid SOAP XML in our document. See if the server said things were successful.

		if(ok)
		{
			return { value: parseResultValue(document, proxy.rtype) };
		}
		else
		{
			// ASMX returns SOAP that looks like this: <soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/" ...
			// WCF  returns SOAP that looks like this: <s:Envelope xmlns:s="http://schemas.xmlsoap.org/soap/envelope/" ...
			// Note that they both use the same namespace, but different prefixes. MSXML automatically allows us to use XPath queries
			// with the same prefixes as are in the document. Our native XML code achieves the same thing using document.createNSResolver.
			// We could get fancy and define our own prefix and namespace for the XPath queries, but that's a pain because MSXML and
			// native do that in two different ways. So we tightly couple ourselves to the prefixes that ASMX and WCF use. As a bonus
			// that lets us easily tell if the SOAP came from WCF or ASMX.

			node = document.selectSingleNode('/soap:Envelope/soap:Body/soap:Fault');	// ASMX

			if(node)
			{
				node = document.selectSingleNode('/soap:Envelope/soap:Body/soap:Fault/detail/Exception');

				if(node)
				{
					return { exception: createCustomException(node) };			// We have custom error information encoded by our SoapExceptionHandler.
				}
				else
				{
					// There was no custom error information encoded in the SOAP response, so this response didn't run through our SoapExceptionHandler.

					if(statusCode === 500)
					{
						if(options && options.ignoreBasicErrors)
						{
							return { value: window.undefined };					// This is a "normal" internal server error so for compatibility we need to return undefined to the caller.
						}
						else
						{
							return { exception: createStatusException(response, statusCode, statusText) };
						}
					}
					else
					{
						return { exception: createStatusException(response, statusCode, statusText) };	// It's some weird error, so let's make a basic exception for it.
					}
				}
			}

			node = document.selectSingleNode('/s:Envelope/s:Body/s:Fault');				// WCF

			if(node)
			{
				detail = document.selectSingleNode('/s:Envelope/s:Body/s:Fault/detail/ExceptionDetail');

				if(detail)
				{
					return { exception: createCustomException(detail) };		// WCF included exception detail
				}
				else
				{
					// For some reason WCF did not include any exception detail. Note that unlike ASMX we don't want to return undefined in this case. That's old behavior we don't want to carry over.

					return { exception: createFaultException(query(document, 'faultcode', node), query(document, 'faultstring', node)) }
				}
			}
		}

		// We're either not talking to ASMX or WCF, or they didn't include a Fault node for some reason. We make a basic exception for that.

		return { exception: createStatusException(response, statusCode, statusText) };
	}

	/* Internal Methods */

	function buildParameter(method, name, type, arrayNS, value)
	{
		// See http://wiki/x/FIFDAg

		var xml, date, isNull = false, isArray = false, attributes = '';

		if(!type)	// strings have a blank type
		{
			if((typeof value === 'undefined') || (value === null))
			{
				xml = '';
			}
			else if(typeof value !== 'string')
			{
				try
				{
					xml = value.toString();
				}
				catch(exception)
				{
					throw new ArgumentException("Parameter '" + name + "' of method '" + method + "' must be a string.");
				}
			}
			else
			{
				xml = value;
			}

			xml = xml.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;');
		}
		else if(type === 'int')
		{
			xml = value ? parseInt(value, 10) : '0';
		}
		else if((type === 'float') || (type === 'double') || (type === 'decimal'))
		{
			xml = value ? parseFloat(value) : '0';
		}
		else if((type === 'bool') && (typeof value === 'string'))
		{
			xml = value.toLowerCase();
		}
		else if(type === 'bool')
		{
			xml = safeStr(value).toLowerCase();
		}
		else if(type === 'date')
		{
			date = toDate(value);

			if(date === null)
			{
				throw new ArgumentException("The value passed for the parameter '" + name + "' of method '" + method + "' is not a valid date/time.");
			}

			xml = formatSoapDate(date);
		}
		else if(type === 'date?')
		{
			if(value)
			{
				date = toDate(value);

				if(date === null)
				{
					throw new ArgumentException("The value passed for the parameter '" + name + "' of method '" + method + "' is not a valid date/time.");
				}

				xml = formatSoapDate(date);
			}
			else
			{
				xml = '';

				isNull = true;
			}
		}
		else if(type.endsWith('[]'))
		{
			if(type === 's[]')
			{
				xml = buildSoapArray(value, 'string');
			}
			else if(type === 'int[]')
			{
				xml = buildSoapArray(value, 'int');
			}
			else if(type === 'float[]')
			{
				xml = buildSoapArray(value, 'float');
			}
			else if(type === 'double[]')
			{
				xml = buildSoapArray(value, 'double');
			}
			else if(type === 'decimal[]')
			{
				xml = buildSoapArray(value, 'decimal');
			}
			else if(type === 'bool[]')
			{
				xml = buildSoapArray(value, 'boolean');
			}
			else
			{
				throw new ArgumentException("Parameter '" + name + "' of method '" + method + "' is an unknown type.");
			}

			isNull = (xml === null);

			isArray = true;
		}
		else if((type === 'x') && (typeof value === 'string'))
		{
			xml = value;
		}
		else if(type === 'x')
		{
			if(value)
			{
				xml = value.xml;
			}
			else
			{
				xml = '';
			}
		}
		else
		{
			throw new ArgumentException("Parameter '" + name + "' of method '" + method + "' is an unknown type.");
		}

		if(isNull)
		{
			attributes += " xsi:nil='true' xmlns:xsi='http://www.w3.org/2001/XMLSchema-instance'";
		}

		if(isArray)
		{
			attributes += " xmlns:a='" + arrayNS + "'";
		}

		xml = '<' + name + attributes + '>' + xml + '</' + name + '>';

		return xml;
	}

	function formatSoapDate(date)
	{
		var value, temp;

		value = safeStr(date.getFullYear()) + '-';

		temp = safeStr(date.getMonth() + 1);
		value += (temp.length === 1 ? '0' + temp : temp) + '-';

		temp = safeStr(date.getDate());
		value += (temp.length === 1 ? '0' + temp : temp) + 'T';

		temp = safeStr(date.getHours());
		value += (temp.length === 1 ? '0' + temp : temp) + ':';

		temp = safeStr(date.getMinutes());
		value += (temp.length === 1 ? '0' + temp : temp) + ':';

		temp = safeStr(date.getSeconds());
		value += (temp.length === 1 ? '0' + '0' : temp);

		temp = date.getMilliseconds();

		if(temp !== 0)
		{
			temp = safeStr(temp);

			value += '.' + (temp.length === 2 ? '0' + temp : (temp.length === 1 ? '00' + temp : temp));
		}

		return value;
	}

	function buildSoapArray(values, nodeName)
	{
		if(values)
		{
			if(values.length > 0)
			{
				// Note that the 'a' namespace is set in buildParameter. ASMX and WCF expect two different things for the namespace.

				return '<a:' + nodeName + '>' + values.join('</a:' + nodeName + '><a:' + nodeName + '>') + '</a:' + nodeName + '>';
			}
			else
			{
				return '';
			}
		}
		else
		{
			return null;
		}
	}

	function parseResultValue(document, responses)
	{
		// See http://wiki/x/FIFDAg
		// responses is an array of strings in the format "name:type"; we only use the first response in the array.

		// When returning an empty string, array or list, ASMX and WCF return this:
		//
		// <ReturnsStringArrayEmptyResponse xmlns="http://www.mediquant.com/DataArk4/">
		//     <ReturnsStringArrayEmptyResult />
		// </ReturnsStringArrayEmptyResponse>

		// When returning a null string, array or list, ASMX and WCF do different things. Note that the nil attribute
		// gets a different namespace prefix if it's a null string versus a null array or list!
		//
		// ASMX: <ReturnsStringNullResponse xmlns="http://www.mediquant.com/DataArk4/" />
		//
		// WCF: <ReturnsStringNullResponse xmlns="http://www.mediquant.com/DataArk4/">
		//          <ReturnsStringNullResult a:nil="true" xmlns:a="http://www.w3.org/2001/XMLSchema-instance"/>
		//      </ReturnsStringNullResponse>
		//
		// WCF: <ReturnsStringArrayNullResponse xmlns="http://www.mediquant.com/DataArk4/">
		//          <ReturnsStringArrayNullResult i:nil="true" xmlns:a="http://schemas.microsoft.com/2003/10/Serialization/Arrays" xmlns:i="http://www.w3.org/2001/XMLSchema-instance" />
		//      </ReturnsStringArrayNullResponse>

		var response, name, type, node, result, index;

		if(responses && responses.length > 0 && responses[0])
		{
			response = responses[0].split(':');

			name = response[0];
			type = response[1];

			node = document.getElementsByTagName(name).item(0);		// This give us the [MethodName]Result element

			if(node === null)
			{
				// Under ASMX, this means there is no [MethodName]Result element. Returning a null string, array or list will come through here.

				return null;
			}
			else if((node.getAttribute('a:nil') === 'true') || (node.getAttribute('i:nil') === 'true'))
			{
				// Under WCF, this means null was returned. Returning a null string, array or list will come through here. Note that we are hard
				// coding the namespace prefix that WCF uses ('a' when it's a string, 'i' when it's an array) which is not the best thing to do.

				return null;
			}
			else if(!type)		// For strings the type is blank.
			{
				if(node.firstChild === null)
				{
					// This means the [MethodName]Result element has no text content. Returning an empty string under ASMX will come through here.

					return '';
				}
				else
				{
					return node.text;
				}
			}
			else if(type === 'int')
			{
				return parseInt(node.text, 10);
			}
			else if((type === 'float') || (type === 'double') || (type === 'decimal'))
			{
				return parseFloat(node.text);
			}
			else if(type === 'bool')
			{
				return node.text.toLowerCase() === 'true';
			}
			else if(type === 'date')
			{
				result = toDate(node.text);

				if(result === null)
				{
					throw new SoapException('The value returned by the web server is not a valid date/time.');
				}

				return result;
			}
			else if(type === 'date?')
			{
				if(node.text)
				{
					result = toDate(node.text);

					if(result === null)
					{
						throw new SoapException('The value returned by the web server is not a valid date/time.');
					}

					return result;
				}
				else
				{
					return null;
				}
			}
			else if(type === 's[]')
			{
				result = [];

				for(index = 0; index < node.childNodes.length; index++)
				{
					result.push(node.childNodes[index].text);
				}

				return result;
			}
			else if(type === 'int[]')
			{
				result = [];

				for(index = 0; index < node.childNodes.length; index++)
				{
					result.push(parseInt(node.childNodes[index].text, 10));
				}

				return result;
			}
			else if((type === 'float[]') || (type === 'double[]') || (type === 'decimal[]'))
			{
				result = [];

				for(index = 0; index < node.childNodes.length; index++)
				{
					result.push(parseFloat(node.childNodes[index].text));
				}

				return result;
			}
			else if(type === 'bool[]')
			{
				result = [];

				for(index = 0; index < node.childNodes.length; index++)
				{
					result.push(node.childNodes[index].text.toLowerCase() === 'true');
				}

				return result;
			}
			else if(type === 'x')
			{
				return $xml.create(node.firstChild.xml);
			}
			else
			{
				throw new SoapException('The return type for this method is unknown.');
			}
		}

		// We fall through here if the method doesn't actually have a return value (is void) and return undefined.
	}

	function createInvalidException(response, statusCode)
	{
		var exception = new SoapException('A request made to the web server was successful but its response was invalid.');

		exception.code = statusCode;

		exception.responseText = response;

		return exception;
	}

	function createFaultException(faultCode, faultString)
	{
		var exception = new SoapException();

		exception.faultCode = faultCode;

		exception.faultString = faultString;

		return exception;
	}

	function createStatusException(response, statusCode, statusText)
	{
		var exception = new SoapException();

		if(statusCode == '12002')
		{
			exception.message = 'Your web browser has timed out and given up waiting for this request.';
		}
		else
		{
			exception.message += ' HTTP Status Code ' + statusCode + ' - ' + statusText + '.';

			if(statusCode == '12152')
			{
				exception.message += ' This usually means a proxy server on your network forced a timeout on the connection before the request completed. See http://wiki/x/coNRAQ';
			}
		}

		exception.description = exception.message;

		exception.code = statusCode;

		exception.responseText = response;

		return exception;
	}

	function createCustomException(node)
	{
		var exception = new SoapException();

		exceptionNodeToProperties(exception, node);

		return exception;
	}

	function exceptionNodeToProperties(exception, parent)
	{
		// WCF uses a Type node for the exception "name" and includes the namespace, while our ASMX extensions use a Name node and includes just the class name.

		var index, node, dataIndex, dataNode, name = null, type = null;

		exception.description = '';

		for(index = 0; index < parent.childNodes.length; index++)
		{
			node = parent.childNodes[index];

			if(node.nodeName === 'StackTrace')
			{
				exception.stacktrace = node.text;
			}
			else if(node.nodeName === 'Data')
			{
				for(dataIndex = 0; dataIndex < node.childNodes.length; dataIndex++)
				{
					dataNode = node.childNodes[dataIndex];

					exception[lcFirst(dataNode.nodeName)] = dataNode.text;
				}
			}
			else if(node.nodeName === 'InnerException')
			{
				if(node.childNodes.length)				// WCF likes to include a blank InnerException
				{
					exception.innerException = exceptionNodeToProperties({}, node);
				}
			}
			else if(node.nodeName === 'Type')
			{
				type = node.text;
			}
			else if(node.nodeName === 'Name')
			{
				name = node.text;
			}
			else if((node.nodeName === 'HelpLink'))		// WCF likes to include a blank HelpLink
			{
				if(node.text)
				{
					exception.helpLink = node.text;
				}
			}
			else if(node.nodeType === 1)	// ELEMENT_NODE
			{
				exception[lcFirst(node.nodeName)] = node.text;
			}
		}

		if(name && !type)
		{
			exception.name = name;
		}
		else if(name && type)
		{
			exception.name = name;

			exception.type = type;
		}
		else if(!name && type)
		{
			exception.name = rightOfLast(type, '.');		// WCF includes the namespace along with the class name. We need to remove that so we get just the class name.
		}

		if((exception.name === 'NoNullAllowedException') || ((exception.name === 'SqlException') && (startsWith(exception.message, 'Invalid column name') || startsWith(exception.message, 'Invalid object name'))))
		{
			exception.message += " DataArk's database schema may be out of date. Please contact MediQuant support.";
		}

		if(!exception.description)
		{
			exception.description = exception.message;
		}

		return exception;
	}
})();

var proxies = (function()
{
	/* Return the Public API */

	return (
	{
		callSoap: callSoap,

		create: create
	});

	/* Public API Methods */

	function callSoap(args)
	{
		var proxy, url, content, xhr, result;

		proxy = args.callee;

		url = proxy.service.url;

		url += (url.contains('?') ? '&' : '?');

		url += 'sid=' + readSessionId() + '&v=' + MediQuant.build;

		content = soap.buildRequest(proxy.fname, proxy.service.ns, proxy.params, args);

		xhr = createXmlHttpRequest();

		xhr.open('POST', url, false); // false means synchronous

		xhr.setRequestHeader('SOAPAction', proxy.action);

		xhr.setRequestHeader('Content-Type', 'text/xml; charset=utf-8');

		xhr.send(content);

		result = soap.parseResponse(xhr.responseXML, xhr.responseText, xhr.status, xhr.statusText, proxy, { ignoreBasicErrors: true });

		if(result.exception)
		{
			throw result.exception;
		}
		else
		{
			return result.value;
		}
	}

	function create(path, ns, method, action, parameters, result)
	{
		return (
		{
			service:
			{
				url: window.location.protocol + '//' + window.location.host + path,

				ns: ns
			},

			fname: method,

			action: action,

			params: parameters,

			rtype: result
		});
	}
})();

var ajax = (function()
{
	/* Return the Public API */

	return (
	{
		send: send,

		queue: queue
	});

	/* Public API Methods */

	function send(proxy)
	{
		if(!proxy)
		{
			throw new ArgumentException('The specified proxies method does not exist.');
		}

		var promise = new Promise();

		var request = new SoapRequest(
		{
			url: proxy.service.url + '?sid=' + readSessionId(),

			action: proxy.action,

			data: soap.buildRequest(proxy.fname, proxy.service.ns, proxy.params, Array.prototype.slice.call(arguments, 1)),

			timeout: proxy.service.timeout,

			onSuccess: onResponse,

			onFailure: onResponse,

			onTimeout: function()
			{
				promise.reject(new AjaxTimeoutException());
			},

			onAbort: function()
			{
				promise.reject(new AjaxAbortException());
			}
		});

		request.send();

		promise.abort = request.abort.bind(request);

		return promise;

		function onResponse(responseXML, responseText, status, statusText)
		{
			var result = soap.parseResponse(responseXML, responseText, status, statusText, proxy);

			if(result.exception)
			{
				promise.reject(result.exception);
			}
			else
			{
				promise.resolve(result.value);
			}
		}
	}

	function queue()
	{
		var newQueue = new AjaxQueue();

		newQueue.queue.apply(newQueue, arguments);

		return newQueue;
	}
})();

var SoapException = function(message)
{
	var exception = new Error();

	exception.name = 'SoapException';

	exception.message = exception.description = message || 'An error occurred while communicating with the web server.';

	exception.exceptionOccurredOn = longDateTime(new Date());

	exception.isSoapException = true;

	return exception;
};

var AjaxAbortException = function()
{
	var exception = new Error();

	exception.name = 'AjaxAbortException';

	exception.message = exception.description = 'The request was canceled.';

	exception.isAbortException = true;

	return exception;
};

var AjaxTimeoutException = function()
{
	var exception = new Error();

	exception.name = 'AjaxTimeoutException';

	exception.message = exception.description = 'The request took longer than was expected and was canceled.';

	exception.isTimeoutException = true;

	return exception;
};

var AjaxQueue = function()
{
	this.requests = [];
};

AjaxQueue.prototype =
{
	queue: function()
	{
		this.requests.push(arguments);

		return this;
	},

	send: function()
	{
		var promises = [];

		this.requests.forEach(function(args, index)
		{
			promises[index] = ajax.send.apply(ajax, args);
		});

		return new Promises(promises);
	}
};

var SoapRequest = function(options)
{
	this.url = versionUrl(options.url);
	this.action = options.action;
	this.data = options.data;
	this.timeout = options.timeout;
	this.onSuccess = options.onSuccess || noop;
	this.onFailure = options.onFailure || noop;
	this.onTimeout = options.onTimeout || noop;
	this.onAbort = options.onAbort || noop;

	this.send = function()
	{
		this.xhr = createXmlHttpRequest();

		this.xhr.open('POST', this.url, true);	// true means asynchronous

		this.xhr.setRequestHeader('SOAPAction', this.action);

		this.xhr.setRequestHeader('Content-Type', 'text/xml; charset=utf-8');

		this.xhr.onreadystatechange = onReadyStateChange.bind(this);

		if(this.timeout)
		{
			this.timer = setTimeout(onTimeout.bind(this), this.timeout);
		}

		this.xhr.send(this.data);
	};

	this.abort = function()
	{
		if(this.xhr)
		{
			this.result = 'abort';

			this.xhr.abort();
		}
	};

	function onReadyStateChange()
	{
		if(this.xhr && this.xhr.readyState === 4)
		{
			if(this.timer)
			{
				clearTimeout(this.timer);
			}

			if(this.result === 'abort')
			{
				this.onAbort();
			}
			else if(this.result === 'timeout')
			{
				this.onTimeout();
			}
			else if((this.xhr.status >= 200 && this.xhr.status <= 299) || (this.xhr.status === 304))
			{
				this.onSuccess(this.xhr.responseXML, this.xhr.responseText, this.xhr.status, this.xhr.statusText);
			}
			else
			{
				this.onFailure(this.xhr.responseXML, this.xhr.responseText, this.xhr.status, this.xhr.statusText);
			}

			this.xhr = null;
		}
	}

	function onTimeout()
	{
		if(this.xhr)
		{
			this.result = 'timeout';

			this.xhr.abort();
		}
	}

	function noop() { }
};
