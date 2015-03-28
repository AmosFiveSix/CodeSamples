// This is the core stuff for our cross browser XML code. The product uses XML heavily on the client side with lots or XPath and XSL.
// It was originally hard coded to the MSXML ActiveX control, so there are calls throughout the code base to MSXML specific methods,
// like SelectSingleNode(). To make getting to cross browser easier, under non-IE browsers we fake the MSXML methods.
// Note that as of this time the product depends on IE 5.5 Quirks mode (grumble) but part of the cross-browser upgrade is to get the
// IE stuff into IE's standards mode. But note that even in IE's standard mode we still use the MSXML ActiveX control because IE
// standards mode does not support XPath (no document.evaluate(), etc.) and even IE 12/Spartan will have limited support.
// Of course Spartan won't allow any ActiveX (I believe) and at start won't have good XPath support so we might not be able to work
// in Spartan at all for a while. ;-)

var $xml = (function()
{
	/* Internal Module-Level Variables */

	var native, internal;

	var nulCharMessage = 'The XML data contains null characters. This usually means data is stored in the database incorrectly. To identify the record, click Details below then search the Source text for "&#x0;".';

	var badCharMessage = 'The XML data contains invalid control characters. These include #x0-#x8, #xB, #xC, #xE-#x1F. This usually means data is stored in the database incorrectly. To identify the record, click Details below then search Source for text beginning with  "&#x".';

	/* IE Internal Methods */

	var ieInit = (function()
	{
		/* Return the Public API */

		return (
		{
			stringToDocument: stringToDocument,

			documentToString: documentToString,

			getParserException: getParserException,

			hasParserException: hasParserException
		});

		/* Public API Methods */

		function stringToDocument(xml, options)
		{
			var progId, document;

			if(options && options.freeThreaded)
			{
				progId = 'Msxml2.FreeThreadedDOMDocument';
			}
			else
			{
				progId = 'Msxml2.DOMDocument';
			}

			if(options && options.version === 6)
			{
				try
				{
					document = new ActiveXObject(progId + '.6.0');		// http://bit.ly/MSXMLVersions and http://bit.ly/BestMSXMLVersion

					document.setProperty('NewParser', true);		// http://bit.ly/NewParser - MSXML 4.0+

					document.setProperty('ProhibitDTD', false);		// http://bit.ly/ProhibitDTD - MSXML 3.0 SP5+ and 6.0. Default false for 3.0, true for 6.0.

					document.setProperty('AllowXsltScript', true);		// http://bit.ly/AllowXsltScript - MSXML 3.0 SP8+ and 6.0. Default true for 3.0, false for 6.0.
				}
				catch(exception)
				{
					document = new ActiveXObject(progId + '.3.0');

					document.setProperty('SelectionLanguage', 'XPath');	// http://bit.ly/SelectionLanguage - MSXML 4.0+ only supports XPath.
				}
			}
			else
			{
				document = new ActiveXObject(progId + '.3.0');

				if(options && options.language === 'XPath')
				{
					document.setProperty('SelectionLanguage', 'XPath');	// http://bit.ly/SelectionLanguage - MSXML 3.0 supports XPath and XSLPattern. 3.0 defaults to XSL Pattern.
				}
			}

			document.async = false;
			document.preserveWhiteSpace = false;
			document.resolveExternals = true;
			document.validateOnParse = false;

			document.loadXML(xml);

			return document;
		}

		function documentToString(node)
		{
			return node.xml;
		}

		function getParserException(document)
		{
			var error = document.parseError;

			if(error.errorCode !== 0)
			{
				var exception = new Error();

				if((error.errorCode === -1072896737) || (error.errorCode === -1072894421))
				{
					exception.type = 'Invalid Character';

					if(error.srcText.indexOf('&#x0;') !== -1)
					{
						exception.message = nulCharMessage;
					}
					else
					{
						exception.message = badCharMessage;
					}
				}
				else
				{
					exception.message = 'The XML data is invalid. ' + trim(error.reason);
				}

				exception.name = 'XmlException';
				exception.number = error.errorCode;
				exception.description = exception.message;

				exception.reason = trim(error.reason);
				exception.line = error.line;
				exception.column = error.linepos;
				exception.source = error.srcText;
				exception.url = error.url;
				exception.helpLink = 'http://wiki/x/KIEm';

				return exception;
			}
			else
			{
				return null;
			}
		}

		function hasParserException(document)
		{
			return document.parseError.errorCode !== 0;
		}
	});

	/* Native Internal Methods */

	var nativeInit = (function()
	{
		/* Internal Module-Level Variables */

		var _parserErrorNamespace;	// Do not access directly. Use getParserErrorNamespace().

		var firefoxParserErrorNamespace = 'http://www.mozilla.org/newlayout/xml/parsererror.xml';

		var chromeParserErrorNamespace = 'http://www.w3.org/1999/xhtml';

		/* Initialization */

		initNodeExtensions();

		/* Return the Public API */

		return (
		{
			stringToDocument: stringToDocument,

			documentToString: documentToString,

			getParserException: getParserException,

			hasParserException: hasParserException
		});

		/* Public API Methods */

		function stringToDocument(xml)
		{
			var parser = new DOMParser();

			return parser.parseFromString(xml, 'application/xml');
		}

		function documentToString(node)
		{
			var serializer = new XMLSerializer();

			return serializer.serializeToString(node);
		}

		function getParserException(document)
		{
			// Firefox and Chrome report errors differently. This is rather brittle. See http://stackoverflow.com/a/11623204/114267.

			var errors, error, exception;

			errors = document.getElementsByTagNameNS(getParserErrorNamespace(), 'parsererror');	// With Firefox there is only one at the root. With Chrome they can be anywhere.

			if(errors.length > 0)
			{
				error = errors[0];

				exception = new Error();

				exception.reason = 'Unknown XML parsing error.';

				exception.type = 'Unknown';

				if(error.namespaceURI === firefoxParserErrorNamespace)
				{
					if(error.childNodes[0])
					{
						exception.reason = error.childNodes[0].nodeValue;					// The first text node inside the parsererror element.
					}

					if(error.childNodes[1] && error.childNodes[1].firstChild)				// The sourcetext element that comes right after the text node.
					{
						exception.source = error.childNodes[1].firstChild.nodeValue;
					}

					exception.message = 'The XML data is invalid. ' + exception.reason;

					if(exception.reason.startsWith('XML Parsing Error: reference to invalid character number'))
					{
						exception.type = 'Invalid Character';

						if(exception.source && exception.source.contains('&#x0;'))
						{
							exception.message = nulCharMessage;
						}
						else
						{
							exception.message = badCharMessage;
						}
					}
				}
				else if(error.namespaceURI === chromeParserErrorNamespace)
				{
					if(error.childNodes[1] && error.childNodes[1].firstChild)
					{
						exception.reason = error.childNodes[1].firstChild.nodeValue;		// The text inside the div element that comes after the h3 element.

						exception.message = 'The XML data is invalid. ' + exception.reason;

						if(exception.reason.contains('xmlParseCharRef: invalid xmlChar value 0'))
						{
							exception.type = 'Invalid Character';

							exception.message = nulCharMessage;
						}
						else if(exception.reason.contains('xmlParseCharRef: invalid xmlChar value'))
						{
							exception.type = 'Invalid Character';

							exception.message = badCharMessage;
						}
					}
				}
				else
				{
					exception.message = 'The XML data is invalid.';
				}

				exception.name = 'XmlException';
				exception.description = exception.message;
				exception.helpLink = 'http://wiki/x/KIEm';

				return exception;
			}
			else
			{
				return null;
			}
		}

		function hasParserException(document)
		{
			var errors = document.getElementsByTagNameNS(getParserErrorNamespace(), 'parsererror');

			return errors.length > 0;
		}

		/* Node Extensions */

		function selectNodes(document, xpath, contextNode)
		{
			var nodes, resolver, result, length, index;

			nodes = [];

			resolver = document.createNSResolver(document.documentElement);

			result = document.evaluate(xpath, contextNode, resolver, XPathResult.ORDERED_NODE_SNAPSHOT_TYPE, null);

			length = result.snapshotLength;

			for(index = 0; index < length; index++)
			{
				nodes[index] = result.snapshotItem(index);
			}

			return nodes;
		}

		function selectSingleNode(document, xpath, contextNode)
		{
			var resolver, result;

			resolver = document.createNSResolver(document.documentElement);

			result = document.evaluate(xpath, contextNode, resolver, XPathResult.FIRST_ORDERED_NODE_TYPE, null);

			return result.singleNodeValue;
		}

		function getText(node)
		{
			var text;

			if(node.nodeType === Node.DOCUMENT_NODE)
			{
				text = node.documentElement.textContent;
			}
			else
			{
				text = node.textContent;
			}

			return trim(text);
		}

		function getXml(node)
		{
			var document = node.ownerDocument || node;

			if(hasParserException(document))
			{
				return '';
			}
			else
			{
				return documentToString(node);
			}
		}

		function initNodeExtensions()
		{
			Object.defineProperty(Node.prototype, 'text',
			{
				get: function()
				{
					return getText(this);
				},

				enumerable: true,

				configurable: false
			});

			Object.defineProperty(Node.prototype, 'xml',
			{
				get: function()
				{
					return getXml(this);
				},

				enumerable: true,

				configurable: false
			});

			Node.prototype.selectNodes = function(xpath)
			{
				return selectNodes(this.ownerDocument || this, xpath, this);
			};

			Node.prototype.selectSingleNode = function(xpath)
			{
				return selectSingleNode(this.ownerDocument || this, xpath, this);
			};
		}

		/* Internal Methods */

		function getParserErrorNamespace()
		{
			if(!_parserErrorNamespace)
			{
				var parser, document;

				parser = new DOMParser();

				document = parser.parseFromString('Invalid', 'application/xml');

				_parserErrorNamespace = document.getElementsByTagName("parsererror")[0].namespaceURI;
			}

			return _parserErrorNamespace;
		}
	});

	/* Initialization */

	native = ((Object.getOwnPropertyDescriptor && Object.getOwnPropertyDescriptor(window, "ActiveXObject")) || ("ActiveXObject" in window)) ? false : true;

	if(native)
	{
		internal = nativeInit();
	}
	else
	{
		internal = ieInit();
	}

	/* Public API */

	return (
	{
		native: native,

		create: function(xmlOrPath, options)
		{
			var xml, document, exception;

			if(xmlOrPath)
			{
				if(isError(xmlOrPath))
				{
					// Don't even ask ;-) Old junk I wish was not around

					throw stringToException(xmlOrPath);
				}

				if(xmlOrPath.charAt(0) === '<')
				{
					xml = xmlOrPath;
				}
				else
				{

					// I don't like this downloading behavior but it's here for legacy compatibility

					xml = httpGet(xmlOrPath);
				}
			}
			else
			{
				xml = '<xml/>';
			}

			document = internal.stringToDocument(xml, options);

			exception = internal.getParserException(document);

			if(exception)
			{
				throw exception;
			}
			else
			{
				return document;
			}
		},

		deserialize: function(xml, options)
		{
			// We purposely only accept non-blank strings that are supposed to be XML and we do not check if the parsing of the XML failed.

			assert.string(xml, "Parameter 'xml' to function $xml.deserialize()");

			return internal.stringToDocument(xml, options);
		},

		serialize: function(node)
		{
			assert.object(node, "Parameter 'node' to function $xml.serialize()");

			return internal.documentToString(node);
		},

		isValid: function(xml)
		{
			if(xml && (typeof xml === 'string'))
			{
				var document = internal.stringToDocument(xml);

				return !internal.hasParserException(document);
			}
			else
			{
				return false;
			}
		},

		getParserException: function(document)
		{
			assert.object(document, "Parameter 'document' to function $xml.getParserException()");

			return internal.getParserException(document);
		},

		hasParserException: function(document)
		{
			assert.object(document, "Parameter 'document' to function $xml.hasParserException()");

			return internal.hasParserException(document);
		},

		clean: function(xml)
		{
			// This cleans the actual characters. See http://wiki/x/KIEm

			xml = xml.replace(new RegExp('[^\x09\x0A\x0D\u0020-\uD7FF\uE000-\uFFFD]', 'g'), '');

			// This cleans encoding of the characters, but only 0x00-0x08, 0x0B, 0x0C, 0x0E, 0x0F, 0x10-0x1F. Obfuscated .NET assemblies can have method names that are control characters like this.

			xml = xml.replace(new RegExp('&#x(?:[0-8b-cB-Ce-fE-F]|1[0-9a-fA-F]);', 'g'), '');

			return xml;
		},

		iterate: function(node, xpath, callback)
		{
			var children, length, index;

			xpath = xpath || '*';

			children = node.selectNodes(xpath);

			length = children.length;

			for(index = 0; index < length; index++)
			{
				callback(children[index], node);
			}
		},

		xPathOf: function(node)
		{
			if(node === node.ownerDocument.documentElement)
			{
				return '/' + node.tagName;
			}
			else
			{
				var siblings = node.parentNode.selectNodes(node.tagName);

				for(var index = 0; index < siblings.length; index++)
				{
					if(siblings[index] === node)
					{
						return $xml.xPathOf(node.parentNode) + '/' + node.tagName + '[' + (index + 1) + ']';
					}
				}

				return null;
			}
		}
	});
})();
