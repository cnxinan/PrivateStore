var localhostReg = new RegExp("^localhost");

var config = {
  env: 'test08', //.e.g test08/test16/localhost
  loginUser: 'cedric.chen@wendywuchina.com', //should change to a common user to excute the url tests.
  loginPwd: '123456',
  basicAuth: new Buffer('wwt:wwt2018').toString('base64')
}

var host = localhostReg.test(config.env) ? config.env + ":7080" : config.env + "backoffice.wwtqin.com";
// var baseUrl = localhostReg.test(config.env) ? "http://"+ config.env : config.env + "backoffice.wwtqin.com";
var baseUrl = localhostReg.test(config.env) ? "http://" + host : "https://" + host;

module.exports = {
  config,
  baseUrl,
  host
};