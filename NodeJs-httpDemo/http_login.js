var https = require('https');
var httpConfig = require('./http_config');

var loginOptions = {
  host: httpConfig.baseUrl,
  path: '/Account/Login',
  // authentication headers
  headers: {
    'authorization': 'Basic ' + new Buffer('wwt:wwt2018').toString('base64')
  },
};

https.get(loginOptions, function (res) {
  cookie_aspnet_session_id = res.headers["set-cookie"][0].split(';')[0];
  cookie_requestVer_token = res.headers["set-cookie"][2].split(';')[0];
  res.on('data', function (d) {
    var postData = JSON.stringify({
      "UserName": httpConfig.loginUser,
      "Password": httpConfig.loginPwd
    });

    let options = {
      host: httpConfig.baseUrl,
      path: '/Account/Login',
      method: 'POST',
      headers: {
        'accept': 'text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3',
        'accept-encoding': 'gzip, deflate, br',
        'accept-language': 'en-US,en;q=0.9',
        'authorization': 'Basic ' + new Buffer('wwt:wwt2018').toString('base64'),
        'cache-control': 'max-age=0',
        'content-length': postData.length,
        'content-type': 'application/json',
        'cookie': cookie_aspnet_session_id + ';' + cookie_requestVer_token,
        'referer': httpConfig.baseUrl,
        'sec-fetch-mode': 'navigat',
        'sec-fetch-site': 'same-origin',
        'upgrade-insecure-requests': '1',
        'user-agent': 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/76.0.3809.132 Safari/537.36'
      },
    };

    var req = https.request(options, function (res1) {
      console.log(res1.headers);
      //cookie_aspx_auth = res1.headers[0].split(';')[0];
    });
    req.write(postData);
    req.on('response', function (response) {
      console.log('success!');
    }).on('error', function (e) {
      console.error(e);
    });
  });
}).on('error', function (e) {
  console.error(e);
});