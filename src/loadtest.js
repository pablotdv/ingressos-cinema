import http from 'k6/http';
import { sleep, check } from 'k6';
import { Counter } from 'k6/metrics';

let chairCounter = new Counter('chair_counter');

export let options = {
  vus: 20, // Número de usuários virtuais simultâneos
  duration: '5m', // Duração do teste
};

export default function () {
  // Incrementa o número da poltrona
  let poltrona = `A${86 + chairCounter.add(1)}`;

  let url = `http://localhost:5269/ingressos/async`;
  let payload = JSON.stringify({
    evento: "Cinema",
    poltrona: poltrona,
    nome: "Pablo",
    cpf: "000.000.000-00",
    email: "pablotdv@gmail.com",
    dataNascimento: "1995-06-26T21:22:59.786Z",
    endereco: "Rua 1, 123, Bairro 2, Cidade 3, Estado 4, CEP 5",
    data: "2024-06-26T21:22:59.786Z",
    valor: 100,
    cartaoCredito: "5407 3878 8523 2669"
  });

  let params = {
    headers: {
      'Content-Type': 'application/json',
    },
  };

  let res = http.post(url, payload, params);

  // Verifica se a resposta foi bem-sucedida
  check(res, {
    'status is 200': (r) => r.status === 200,
    'status is 400': (r) => r.status === 400,
  });

  // Aguarda um pouco entre as requisições
  sleep(1);
}
