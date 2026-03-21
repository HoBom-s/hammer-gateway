@Library('hobom-shared-lib') _
hobomPipeline(
  serviceName:    'dev-hammer-gateway',
  hostPort:       '5000',
  containerPort:  '8080',
  memory:         '256m',
  cpus:           '0.5',
  envPath:        '/etc/hobom-dev/dev-hammer-gateway/.env',
  addHost:        true,
  submodules:     false,
  smokeCheckPath: '/health'
)
