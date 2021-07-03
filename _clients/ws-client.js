const readline = require('readline');
const WebSocket = require('ws');

let client = null;

// INSTRUCTIONS

console.log();
console.log('----- INSTRUCTIONS -----');
console.log('exit: close application');
console.log('cls: clear console');
console.log('server {endpoint}: connect to server');
console.log('- server ws://192.168.0.1:8002');
console.log('send {text}: send message to server');
console.log('- send hello world');
console.log();

// INPUT

const rl = readline.createInterface(process.stdin, process.stdout);
rl.setPrompt('> ');
rl.prompt();
rl.on('line', line => {
    try {
        if (/server (.*)/.test(line)) {
            const endpoint = line.split(' ')[1];
            reconnect(endpoint);
        } else if (/send (.*)/.test(line)) {
            if (client == null) {
                console.log('- first connect to server -');
            } else {
                const msg = line.match(/send (.*)/)[1];
                client.send(msg);
            }
        } else if (line === 'cls') {
            console.clear();
        } else if (line === 'exit') {
            exitHandler(true);
        } else if (line.trim() !== '') {
            console.log('- invalid command -');
        }
    } catch (err) {
        console.log('Error:', err);
    }

    rl.prompt();
}).on('close',function(){
    exitHandler.bind(null, false);
});

// OUTPUT

function reconnect(endpoint) {
    if (client != null) client.close();

    const prefix = `\r${endpoint} >> `;
    const sulfix = '\n> ';

    client = new WebSocket(endpoint);
    
    client.on('open', () => process.stdout.write(prefix + 'opened' + sulfix));
    
    client.on('message', data => process.stdout.write(prefix + data + sulfix));
    
    client.on('error', err => process.stdout.write(prefix + err + sulfix));
    
    client.on('close', (code, reason) => process.stdout.write(prefix + `closed | ${code} | ${reason}` + sulfix));
}

// EXIT HANDLING

function exitHandler(exit, exitCode) {
    if (client != null) client.close();
    if (exit) process.exit();
}

// do something when app is closing
process.on('exit', exitHandler.bind(null, false));

// catches ctrl+c event
process.on('SIGINT', exitHandler.bind(null, true));

// catches "kill pid" (for example: nodemon restart)
process.on('SIGUSR1', exitHandler.bind(null, true));
process.on('SIGUSR2', exitHandler.bind(null, true));

// catches uncaught exceptions
process.on('uncaughtException', exitHandler.bind(null, true));