# FAH

Equalizador, efeitos, Som 7.1 para fone e tratamento de microfone para todo o som do Windows.

O FAH recebe o som dos programas por um cabo virtual, aplica os ajustes e entrega na sua saída real. O mesmo vale para o microfone: ele limpa a sua voz e entrega para o Discord, jogos e outros programas por um segundo cabo virtual.

## O que ele faz

- **Equalizador** gráfico e paramétrico, com tom e limitador.
- **AutoEQ** com 8.850 correções para 6.033 modelos de fone, a partir do projeto [AutoEq](https://github.com/jaakkopasanen/AutoEq).
- **Efeitos:** balanço, largura do estéreo, reverb e crossfeed.
- **Som 7.1 para fone:** transforma os 8 canais que os jogos mandam em som posicionado, com medições de cabeça MIT KEMAR e SADIE II.
- **Microfone:** limpeza de ruído com IA (DeepFilterNet3), detecção de voz (Silero VAD), equalizador e alteração de voz.
- **Mixer:** volume por programa, cópia do som de um programa para outra saída, desvio direto para um aparelho e envio do som de um programa para o microfone.

## Como usar

1. Instale um cabo virtual (tabela abaixo). Sem ele o FAH avisa e não liga o som.
2. Baixe `FAH-win-x64.zip` em [Releases](https://github.com/finjackin1/FAH/releases).
3. Extraia a pasta onde quiser e abra `FAH.exe`. Não precisa instalar. O Windows pode alertar na primeira vez porque o programa não tem assinatura digital.

Em uma pasta sem permissão de escrita, como `Arquivos de Programas`, os ajustes ficam em `%LOCALAPPDATA%\FAH`. Nas outras, ficam ao lado do programa.

## Requisitos

- Windows 10 versão 2004 ou mais novo, 64 bits. O Windows em ARM não é suportado.
- Microsoft Edge WebView2 Runtime. Já vem no Windows 11. Se faltar, o FAH avisa e abre a página de download.
- Um cabo virtual:

| Cabo | Som | Som 7.1 dos jogos | Microfone |
|---|---|---|---|
| [Virtual Audio Cable](https://vac.muzychenko.net/) (pago, não o Lite) | Sim | Sim | Sim, com `Cables` em 2 no painel do VAC |
| [VB-CABLE](https://vb-audio.com/Cable/) | Sim | Não | Só com o CABLE-A instalado junto |
| [Voicemeeter](https://vb-audio.com/Voicemeeter/) | Sim | Não | Não |

Na primeira vez que o FAH coloca o Virtual Audio Cable em 7.1, o Windows pode pedir permissão de administrador. Se você recusar, o cabo fica em estéreo.

Ao fechar, o FAH desliga os cabos virtuais no Windows e devolve a saída padrão. Os programas abertos podem ficar sem som até serem reabertos. Os cabos voltam quando o FAH abre de novo. Para desinstalar, abra e feche o FAH uma última vez e apague a pasta; se um cabo continuar sumido, ative-o de novo em Configurações > Sistema > Som.

## Como compilar

Precisa do [SDK do .NET 10](https://dotnet.microsoft.com/download) e do Python 3.

```
python ferramentas/empacotar.py <pasta vazia>
```

O resultado sai em `<pasta vazia>\FAH` e em `FAH-win-x64.zip`. Os testes ficam em `fonte/Testes`. Alguns precisam de amostras de áudio que não estão no repositório e de um cabo virtual instalado.

## Licença

GPLv3, veja [LICENSE](LICENSE). Componentes de terceiros em [THIRD-PARTY-NOTICES.txt](THIRD-PARTY-NOTICES.txt).

O FAH não tem ligação com os fabricantes de Virtual Audio Cable, VB-CABLE, Voicemeeter ou com o projeto AutoEq.
