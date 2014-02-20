var app = require('express')();
var server = require('http').Server(app);
//var server = http.createServer(app)
var io = require('socket.io').listen(server);
//io.set('log level',5); 

io.on('connection', function(socket){
  


  socket.on('event', function(data){
    console.log('event');
  });
  socket.on('message', function(data){
    console.log('In message');
    console.log(data);
    socket.emit ('messageName', { arg1 : 'foo' });
    socket.emit('news_response', { hello : 'world'});
  });
  socket.on('anything', function(data){
    console.log('anything');
  });
  socket.on('news', function (){
    console.log('news received, emitting news_response now');
    socket.emit('news_response', { hello : 'world'});
  });

  socket.on('foo', function (data){
    console.log(data);
  });

  socket.on('disconnect', function(){});
});
 
// io.on('event', function(data){
//     console.log('event');
// });
// io.on('message', function(data){
//     console.log('message');
// });
// io.on('anything', function(data){
//     console.log('anything');
// });

server.listen(3000);


// app.get('/', function (req, res) {
//   res.sendfile(__dirname + '/index.html');
// });

////io.set('browser client minification',true);
////io.set('browser client gzip',true);


// io.sockets.on('connection', function (socket) {
//   socket.emit('news', { hello: 'world' });
//   socket.on('my other event', function (data) {
//     console.log(data);
//   });
// });


// io.sockets.on('connection', function (socket) {
//   socket.emit('news', { hello: 'world' });
  
//   socket.on('my other event', function (data) {
//     console.log(data);
//   });
  
//   socket.on('messageAck', function (data, fn) {
//     console.log('messageAck: ' + data);
// 	console.log(fn);
// 	fn({ hello: 'world' });
//   });
// });
