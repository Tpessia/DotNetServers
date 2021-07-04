const readline = require('readline');
const axios = require('axios');

let endpoint = null;
let method = null;

// INSTRUCTIONS

console.log();
console.log('----- INSTRUCTIONS -----');
console.log('exit: close application');
console.log('cls: clear console');
console.log('server {endpoint} {method}: set server\'s endpoint and method');
console.log('- server http://192.168.0.1:8010 POST');
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
            endpoint = line.split(' ')[1];
            method = line.split(' ')[2];
        } else if (/send (.*)/.test(line)) {
            if (endpoint == null) {
                console.log('- first set an endpoint -');
            } else if (method == null) {
                console.log('- first set a method -');
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
    const prefix = `\r${endpoint} >> `;
    const sulfix = '\n> ';

    axios.request({
        method: 'POST',
        url: endpoint,
        data: msg
    }).then(
        r => process.stdout.write(prefix + r.data + sulfix),
        err => process.stdout.write(prefix + err + sulfix)
    );
}