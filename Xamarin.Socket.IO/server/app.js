var app = require('express')();
var server = require('http').Server(app);
var io = require('socket.io').listen(server);

io.on('connection', function(socket){

    socket.on('message', function(data){
	console.log('received: ' + JSON.stringify(data));
	socket.emit('news_response', { hello : 'world'});
    });

    socket.on('news', function (data){
	console.log('received news');
	socket.emit('news_response', { hello : 'world'});
	socket.json.send({foo:'json'});
	socket.send('ThisIsAMessage');
    });
    
    socket.on('disconnect', function(){
	console.log('diconnected');
    });

});

server.listen(3000);
