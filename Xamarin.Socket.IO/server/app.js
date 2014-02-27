var app = require('express')();
var server = require('http').Server(app);
var io = require('socket.io').listen(server);

io.on('connection', function(socket){

  socket.on('message', function(data){
    console.log('In message');
    console.log(data);
    socket.emit ('messageName', { arg1 : 'foo' });
    socket.emit('news_response', { hello : 'world', ok : 'thisWorks'});
  });

  socket.on('news', function (data){
    console.log(data);
    console.log('news received, emitting news_response now');
    socket.emit('news_response', { hello : 'world'});
    socket.json.send({foo:'json'});
    socket.send('ThisIsAMessage');
  });

  socket.on('anything', function (data) {
    console.log ('In anything' + data);
  });

  socket.on('disconnect', function(){});
});
 
server.listen(3000);
