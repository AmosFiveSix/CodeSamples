// This is the JavaScript for a fairly basic tree control. You can use JSON or XML to load the tree.

Sample.Tree = function(id, options)
{
	var instance, subscriptions, domRoot, selection;

	instance = this;

	initialize();

	// Set the Public Interface

	this.subscribe = subscriptions.subscribe.bind(subscriptions);

	this.unsubscribe = subscriptions.unsubscribe.bind(subscriptions);

	this.enable = function()
	{
		enable(id);
	};

	this.disable = function()
	{
		disable(id);
	};

	this.getSelection = function()
	{
		return selection;
	};

	this.setSelection = function(id)
	{
		descendDom(domRoot, function(dom)
		{
			if(dom.getAttribute && dom.getAttribute('data-id') === id)
			{
				selectItem(dom, true);

				return true;
			}
		});
	};

	this.load = function(tree)
	{
		if(typeof tree === 'string')
		{
			internalLoad(instance.xmlToTree(tree));
		}
		else
		{
			internalLoad(tree);
		}
	};

	this.append = function(subTree, optionalParentId)
	{
		if(typeof subTree === 'string')
		{
			internalAppend(instance.xmlToTree(subTree), optionalParentId);
		}
		else
		{
			internalAppend(subTree, optionalParentId);
		}
	};

	this.xmlToTree = function(stringOrDocumentOrNode, options)
	{
		var node;

		if(typeof stringOrDocumentOrNode === 'string')
		{
			node = $xml.create(stringOrDocumentOrNode).documentElement;
		}
		else if(stringOrDocumentOrNode.documentElement)
		{
			node = stringOrDocumentOrNode.documentElement;
		}
		else
		{
			node = stringOrDocumentOrNode;
		}

		options = options || {};

		options.id = options.id || 'id';

		options.label = options.label || 'label';

		options.elements = options.elements || '*';

		return [xmlNodeToNode(node, options)];
	};

	this.loadXml = function(stringOrDocumentOrNode, options)
	{
		internalLoad(instance.xmlToTree(stringOrDocumentOrNode, options));
	};

	this.clear = function()
	{
		internalLoad();
	};

	// Private Methods

	function initialize()
	{
		options = options || {};

		subscriptions = new Sample.Subscriptions();

		domRoot = getElement(id);

		selection = null;

		if(domRoot.firstChild)
		{
			attachEvents();
		}
	}

	function internalLoad(tree)
	{
		tree = tree || [];

		domRoot.innerHTML = nodesToHtml(tree);

		selection = null;

		attachEvents();
	}

	function internalAppend(subTree, parentId)
	{
		var subTreeHtml, subTreeDom, listDom;

		if(parentId)
		{
			var dom = getItemDom(parentId);	// Returns the LI dom node.

			if(dom)
			{
				listDom = dom.childNodes[2];
			}
			else
			{
				throw new ArgumentException('Unknown parent id.');
			}
		}
		else
		{
			listDom = domRoot.firstChild;
		}

		subTreeHtml = nodeToHtml(subTree);

		subTreeDom = htmlToDom(subTreeHtml);

		listDom.appendChild(subTreeDom);
	}

	function attachEvents()
	{
		addEvent(domRoot.firstChild, 'click', onClick);
		addEvent(domRoot.firstChild, 'dblclick', onClick);
	}

	function onClick(e)
	{
		var src = e.srcElement;

		if(src)
		{
			if(src.tagName === 'DIV' && src.parentNode.className !== 'leaf')
			{
				toggleItem(src.parentNode);
			}
			else if(src.tagName === 'SPAN')
			{
				selectItem(src.parentNode);
			}
			else if(src.tagName === 'A')
			{
				selectItem(src.parentNode.parentNode);
			}
		}
	}

	function selectItem(item, noEvents)
	{
		descendDom(domRoot, function(dom)
		{
			if(dom.tagName === 'SPAN')
			{
				dom.className = '';
			}
		});

		item.childNodes[1].className = 'selection';

		setItemState(item, 'expanded');

		selection = item.getAttribute('data-id');

		if(!noEvents)
		{
			subscriptions.publish('onclick', instance, selection);

			raise(id + 'Clicked');
		}
	}

	function toggleItem(item)
	{
		setItemState(item, (item.className === 'expanded') ? 'collapsed' : 'expanded');
	}

	function setItemState(item, state)
	{
		if(item.className !== 'leaf')
		{
			item.childNodes[2].style.display = (state === 'expanded' ? 'block' : 'none');

			item.className = state;
		}
	}

	function getItemDom(id)
	{
		var result = null;

		descendDom(domRoot, function(dom)
		{
			if(dom.getAttribute && dom.getAttribute('data-id') === id)
			{
				result = dom;

				return true;
			}
		});

		return result;
	}

	function nodesToHtml(nodes)
	{
		var html = '';

		if(nodes && nodes.length)
		{
			html += '<OL>';

			for(var index = 0; index < nodes.length; index++)
			{
				html += nodeToHtml(nodes[index]);
			}

			html += '</OL>';
		}

		return html;
	}

	function nodeToHtml(node)
	{
		var html = '', include, id, label, spanContent;

		include = node.label || options.blanks !== 'ignore';

		if(include)
		{
			id = escapeAttribute(node.id);

			spanContent = label = escapeContent(node.label);

			if(node.format === 'link')
			{
				spanContent = '<A class="link" href="javascript: void(0);">' + label + '</A>';
			}

			html += '<LI data-id="' + id + '" class="' + (node.children && node.children.length ? 'expanded' : 'leaf') + '">';

			html += '<DIV></DIV><SPAN title="' + label + '">' + spanContent + '</SPAN>';
		}

		html += nodesToHtml(node.children);

		if(include)
		{
			html += '</LI>';
		}

		return html;
	}

	function xmlNodeToNode(xmlNode, options)
	{
		var node, xmlNodes, index;

		node = { children: [] };

		node.label = xmlNode.getAttribute(options.label);

		if(options.id === 'xpath')
		{
			node.id = $xml.xPathOf(xmlNode);
		}
		else
		{
			node.id = xmlNode.getAttribute(options.id);
		}

		xmlNodes = xmlNode.selectNodes(options.elements);

		for(index = 0; index < xmlNodes.length; index++)
		{
			node.children.push(xmlNodeToNode(xmlNodes[index], options));
		}

		if(node.children.length === 0)
		{
			delete node.children;
		}

		return node;
	}
};

Sample.Tree.prototype = {};