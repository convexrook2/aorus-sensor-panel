pipeline {
    agent { label 'windows' }
    options {
        skipDefaultCheckout(true)
        disableConcurrentBuilds()
        timeout(time: 45, unit: 'MINUTES')
        timestamps()
        buildDiscarder(logRotator(numToKeepStr: '20', artifactNumToKeepStr: '5'))
    }
    parameters {
        booleanParam(name: 'BUILD_INSTALLER', defaultValue: false,
            description: 'Explicit release build: compile and archive the installer. Never installs or publishes it.')
        string(name: 'EXPECTED_SHA', defaultValue: '', trim: true,
            description: 'Full pushed SHA for an exact-commit closeout; leave blank only for scheduled main checks.')
    }
    triggers { cron('H H * * *') }
    environment {
        CI = 'true'
        DOTNET_NOLOGO = 'true'
        DOTNET_CLI_TELEMETRY_OPTOUT = 'true'
        NUGET_XMLDOC_MODE = 'skip'
    }
    stages {
        stage('Clean checkout') {
            steps {
                // This is Jenkins' dedicated workspace, never the owner's checkout.
                deleteDir()
                script {
                    def revision = checkout scm
                    env.CI_CHECKOUT_SHA = (revision.GIT_COMMIT ?: '').trim().toLowerCase()
                }
            }
        }
        stage('Source identity') {
            steps {
                script {
                    def actual = bat(returnStdout: true, script: '@git rev-parse HEAD').trim().toLowerCase()
                    def expected = (params.EXPECTED_SHA ?: '').trim().toLowerCase()
                    env.CI_EXPECTED_SHA = expected
                    env.CI_ACTUAL_SHA = actual
                    if (expected && !(expected ==~ /^[0-9a-f]{40}$/)) {
                        error('EXPECTED_SHA must be a full 40-character commit SHA.')
                    }
                    if (!(actual ==~ /^[0-9a-f]{40}$/) || actual != env.CI_CHECKOUT_SHA || (expected && actual != expected)) {
                        error('SCM / requested / actual source revision mismatch.')
                    }
                    currentBuild.description = "#${env.BUILD_NUMBER} ${actual.take(12)} ${expected ? 'exact SHA' : 'scheduled main'} | installer=${params.BUILD_INSTALLER}"
                    echo "JENKINS CHECKOUT SHA: ${env.CI_CHECKOUT_SHA}"
                    echo "ACTUAL CHECKED-OUT SHA: ${actual}"
                    echo "EXPECTED PUSHED SHA: ${expected ?: 'not requested (scheduled main tip)'}"
                }
            }
        }
        stage('Build, sensor contracts and protected asset checks') {
            steps {
                bat 'pwsh.exe -NoLogo -NoProfile -NonInteractive -File .\\tools\\Run-JenkinsChecks.ps1'
            }
        }
        stage('Requested release installer') {
            when { expression { params.BUILD_INSTALLER } }
            steps {
                bat 'pwsh.exe -NoLogo -NoProfile -NonInteractive -File .\\tools\\Build-Installer.ps1'
            }
        }
    }
    post {
        always {
            bat '@if not exist artifacts\\ci mkdir artifacts\\ci'
            script {
                catchError(buildResult: 'FAILURE', stageResult: 'FAILURE') {
                    junit allowEmptyResults: false, testResults: 'artifacts/ci/*-tests.xml'
                }
                writeFile file: 'artifacts/ci/source-identity.txt',
                    text: "BUILD_NUMBER=${env.BUILD_NUMBER}\nEXPECTED_SHA=${env.CI_EXPECTED_SHA ?: 'UNSPECIFIED'}\nJENKINS_CHECKOUT_SHA=${env.CI_CHECKOUT_SHA ?: 'UNAVAILABLE'}\nACTUAL_CHECKED_OUT_SHA=${env.CI_ACTUAL_SHA ?: 'UNAVAILABLE'}\nRESULT=${currentBuild.currentResult}\n"
                writeFile file: 'artifacts/ci/jenkins-result.txt',
                    text: "BUILD_NUMBER=${env.BUILD_NUMBER}\nEXPECTED_SHA=${env.CI_EXPECTED_SHA ?: 'UNSPECIFIED'}\nSHA=${env.CI_ACTUAL_SHA ?: 'UNAVAILABLE'}\nRESULT=${currentBuild.currentResult}\n"
            }
            archiveArtifacts allowEmptyArchive: true, fingerprint: true,
                artifacts: 'artifacts/ci/**/*.json,artifacts/ci/**/*.txt,artifacts/ci/*-tests.xml,artifacts/installer/*.exe,artifacts/installer/SHA256SUMS.txt,artifacts/installer/package-manifest.json,artifacts/installer-build.txt'
        }
    }
}
