var https = require('https');
var httpConfig = require('./http_config');
var httpUrls = require('./http_urls');

var cookie_aspnet_session_id;
var cookie_requestVer_token;
var cookie_aspx_auth;

var loginOptions = {
  host: 'test08backoffice.wwtqin.com',
  path: '/Account/Login',
  // authentication headers
  headers: {
    authorization: 'Basic ' + httpConfig.config.basicAuth
  },
};

https.get(loginOptions, function (res) {
  res.on('data', function (d) {
    cookie_aspnet_session_id = res.headers["set-cookie"][0].split(';')[0];
    cookie_requestVer_token = res.headers["set-cookie"][2].split(';')[0];
    var postData = JSON.stringify({
      "UserName": "cedric.chen@wendywuchina.com",
      "Password": "123456"
    });
    let options = {
      host: 'test08backoffice.wwtqin.com',
      path: '/Account/Login',
      method: 'POST',
      headers: {
        'accept': 'text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3',
        'accept-encoding': 'gzip, deflate, br',
        'accept-language': 'en-US,en;q=0.9',
        'authorization': 'Basic ' + httpConfig.config.basicAuth,
        'cache-control': 'max-age=0',
        'content-length': postData.length,
        'content-type': 'application/json',
        'cookie': cookie_aspnet_session_id + ';' + cookie_requestVer_token,
        'referer': 'https://test08backoffice.wwtqin.com/',
        'sec-fetch-mode': 'navigat',
        'sec-fetch-site': 'same-origin',
        'upgrade-insecure-requests': '1',
        'user-agent': 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/76.0.3809.132 Safari/537.36'
      },
    };

    var req = https.request(options, function (res1) {});
    req.write(postData);
    req.on('response', function (response) {
      console.log('login success!');
      cookie_aspx_auth = response.headers['set-cookie'][0].split(';')[0];
      loginOptions.headers.cookie = options.headers.cookie + ";" + cookie_aspx_auth;
      if (httpUrls && httpUrls.length > 0) {
        for (let testUrl in httpUrls) {
          let url = httpUrls[testUrl];
          loginOptions.path = url.url;
          loginOptions.method = 'GET';
          https.get(loginOptions, function (res) {
            console.log(res.statusCode);
            res.on('data', (d) => {
              if (d.toString().indexOf('Something happened while processing your request, please contact technical support.') === -1) {
                console.log("Right Page!");
              } else {
                console.log(loginOptions.path + ": Wrong Page!");
              }
            });

            res.on('error', (err) => {
              console.log('error:' + err);
            })
          });
        };
      }
    });
  });

}).on('error', function (e) {
  console.error(e);
});