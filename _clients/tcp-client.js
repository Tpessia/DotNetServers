const readline = require('readline');
const net = require('net');

let host = null;
let port = null;

// INSTRUCTIONS

console.log();
console.log('----- INSTRUCTIONS -----');
console.log('exit: close application');
console.log('cls: clear console');
console.log('server {host} {port}: set server\'s host and port');
console.log('- server 192.168.0.1 8000');
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
            host = line.split(' ')[1];
            port = line.split(' ')[2];
        } else if (/send (.*)/.test(line)) {
            if (host == null) {
                console.log('- first set a host -');
            } else if (port == null) {
                console.log('- first set a port -');
            } else {
                const msg = line.match(/send (.*)/)[1];
                send(msg);
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
    process.exit(0);
});

// OUTPUT

function send(msg) {
    const prefix = `\r${host}:${port} >> `;
    const sulfix = '\n> ';

    const client = new net.Socket();

    client.connect(port, host, () => {
        process.stdout.write(prefix + 'opened' + sulfix);
        client.write(msg);
    });
    
    client.on('data', data => {
        process.stdout.write(prefix + data + sulfix);
        client.destroy(); // kill client after server's response
    });
    
    client.on('close', () => process.stdout.write(prefix + 'closed' + sulfix));
}