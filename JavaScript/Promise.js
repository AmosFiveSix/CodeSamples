// This is my home grown promise implementation. Note that I kept it simple so both the provider and consumer API are on the same object. Not ideal
// but it keeps the code easier to understand.


var Promise = function()
{
	this.state = 'pending';

	this.callbacks = [];

	this.result = null;
};

Promise.resolved = function(result)
{
	return (new Promise()).resolve(result);
};

Promise.rejected = function(result)
{
	return (new Promise()).reject(result);
};

Promise.prototype =
{
	// Promise Consumer API

	then: function(resolved, rejected)
	{
		if(this.state === 'resolved')
		{
			if(resolved)
			{
				resolved(this.result);
			}
		}
		else if(this.state === 'rejected')
		{
			if(rejected)
			{
				rejected(this.result);
			}
		}
		else
		{
			this.callbacks.push(
			{
				'resolved': resolved,

				'rejected': rejected
			});
		}

		return this;
	},

	always: function(callback)
	{
		return this.then(callback, callback);
	},

	done: function(callback)
	{
		return this.then(callback, null);
	},

	fail: function(callback)
	{
		return this.then(null, callback);
	},

	isResolved: function()
	{
		return this.state === 'resolved';
	},

	isRejected: function()
	{
		return this.state === 'rejected';
	},

	// Promise Provider API

	resolve: function(result)
	{
		return this.complete('resolved', result);
	},

	reject: function(result)
	{
		return this.complete('rejected', result);
	},

	complete: function(state, result)
	{
		var callback;

		if(this.state === 'pending')
		{
			this.state = state;

			this.result = result;

			while(this.callbacks.length)
			{
				callback = this.callbacks.shift();

				if(callback[state])
				{
					callback[state](result);
				}
			}
		}

		return this;
	}
};

var Promises = function(promises)
{
	var self = this;

	var results = [];

	var pending = promises.length;

	Promise.call(this);

	promises.forEach(function(promise, index)
	{
		promise.then(function(result) { collect(true, result); }, function(result) { collect(false, result); });

		function collect(isResolved, result)
		{
			results[index] = { isResolved: isResolved, isRejected: !isResolved, result: result };

			pending--;

			if(pending === 0)
			{
				for(var i = 0; i < results.length; i++)
				{
					if(results[i].isRejected)
					{
						results.rejection = results[i].result;

						break;
					}
				}

				if(results.rejection && self.rejectOnAnyFailure)
				{
					self.reject(results.rejection);
				}
				else
				{
					self.resolve(results);
				}
			}
		}
	});
};

Promises.wait = function(promisesArray, rejectOnAnyFailure)
{
	var promises;

	promises = new Promises(promisesArray);

	promises.rejectOnAnyFailure = rejectOnAnyFailure;

	return promises;
};

Promises.assertResults = function(results)
{
	var result;

	for(var i = 0; i < results.length; i++)
	{
		result = results[i].result;

		if(results[i].isRejected)
		{
			throw result;
		}
		else if(isError(result))
		{
			throw stringToException(result);
		}
	}
};

Promises.prototype = Promise.prototype;
