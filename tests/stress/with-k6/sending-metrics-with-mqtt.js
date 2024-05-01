// k6-with-mqtt run sending-metrics-with-mqtt.js
// Based on https://github.com/pmalhaire/xk6-mqtt/blob/main/examples/test.js

import { fail } from 'k6';

const mqtt = require('k6/x/mqtt');

export const options = {
	scenarios: {
        O1_coldStart:  { executor: 'per-vu-iterations', startTime: '0s',   maxDuration: '1s',  gracefulStop: '1s', vus: 1,  iterations: 1 },
        /*O2_oneDevice:  { executor: 'per-vu-iterations', startTime: '1s',   maxDuration: '90s', gracefulStop: '1s', vus: 1,  iterations: 1 },
        O3_tenDevices: { executor: 'per-vu-iterations', startTime: '100s', maxDuration: '90s', gracefulStop: '1s', vus: 10, iterations: 1 }*/
    }
};

const brokerConfig = { userName: "guest", userPassword: "guest", url: "broker.hivemq.com:1883" };
const mqttConfig = { clientId: "PLC001", useCleanSession: false, connectTimeout: 2000, publishTimeout: 2000, closeTimeout: 2000, qualityOfServiceLevel: 0, retainPolicy: false };

const startUpTestingScenario = () => {
    try {
        const publisher = new mqtt.Client([brokerConfig.url], brokerConfig.userName, brokerConfig.userPassword, mqttConfig.useCleanSession,
                                          mqttConfig.clientId, mqttConfig.connectTimeout);
        publisher.connect();
        return publisher;
    }
    catch (error) {
        fail(`Could not connect to broker for publish: '${error}'`);
    }
};

const executeTestScenario = withPublisher => {
    const targetBrokerTopic = "";
    const messageToSend = "";

    try {
        withPublisher.publish(targetBrokerTopic, mqttConfig.qualityOfServiceLevel, messageToSend, mqttConfig.retainPolicy, mqttConfig.publishTimeout,
                              onSuccessPublishingMessage, onFailurePublishingMessage);
    } catch (error) {
        console.error("publishing error: ", error);
    }
};

const onSuccessPublishingMessage = publishedEvent => {
    // publishedEvent.type
    // publishedEvent.topic
};

const onFailurePublishingMessage = error => {
    // error.type
    // error.message
};

const finishTestingScenario = withPublisher => {
    withPublisher.close(mqttConfig.closeTimeout);
};

export default function () {
    const publisherForTestingSession = startUpTestingScenario();
    executeTestScenario(publisherForTestingSession);
    finishTestingScenario(publisherForTestingSession);
}
