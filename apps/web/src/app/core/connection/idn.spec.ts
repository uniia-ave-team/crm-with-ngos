import { hostnameToUnicode, serverAddressToUnicode } from './idn';

describe('hostnameToUnicode', () => {
  it.each([
    ['xn--j1amh', 'укр'],
    ['xn--80aikifvh.xn--j1amh', 'приклад.укр'],
    ['xn--b1a2a5b3a.xn--j1amh', 'явір.укр'],
    ['xn----7sbbvvcui2trv.xn--j1amh', 'їжак-ґанок.укр'],
    ['crm.xn--80aikifvh.xn--j1amh', 'crm.приклад.укр'],
    ['xn--80aahr3ado7cve2cd.example.org', 'організація.example.org'],
    ['xn--bcher-kva.de', 'bücher.de'],
    ['xn--r8jz45g.jp', '例え.jp'],
    ['XN--J1AMH', 'укр'],
  ])('decodes %s as %s', (encoded, readable) => {
    expect(hostnameToUnicode(encoded)).toBe(readable);
  });

  it.each([['localhost'], ['crm.example.org'], ['192.168.0.7'], ['']])('leaves %j as it is: there is nothing to decode', (hostname) => {
    expect(hostnameToUnicode(hostname)).toBe(hostname);
  });

  it.each([['xn--!!!.org'], ['xn--80aikifv_.org']])('leaves a damaged part as it is (%s)', (hostname) => {
    expect(hostnameToUnicode(hostname)).toBe(hostname);
  });
});

describe('serverAddressToUnicode', () => {
  it.each([
    ['https://xn--80aikifvh.xn--j1amh:5065', 'https://приклад.укр:5065'],
    ['http://crm.xn--80aikifvh.xn--j1amh', 'http://crm.приклад.укр'],
    ['xn--80aikifvh.xn--j1amh', 'приклад.укр'],
    ['http://localhost:5065', 'http://localhost:5065'],
    ['http://192.168.0.9:5065', 'http://192.168.0.9:5065'],
    ['приклад.укр', 'приклад.укр'],
    ['', ''],
  ])('shows %j as %j', (address, readable) => {
    expect(serverAddressToUnicode(address)).toBe(readable);
  });
});
