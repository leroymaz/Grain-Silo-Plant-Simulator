const $ = (selector, root=document) => root.querySelector(selector);
const $$ = (selector, root=document) => [...root.querySelectorAll(selector)];
const fmt = n => Number(n).toLocaleString(undefined,{maximumFractionDigits:1});

const state = {
  plantRunning:true,
  emergencyStop:false,
  activeFault:null,
  xp:0,
  credits:250,
  dryerSetpoint:65,
  furnaceSetpoint:180,
  receipts:1050,
  dispatches:0,
  losses:0,
  logs:[],
  missionCompleted:new Set(),

  nodes:[
    {id:'receiving',name:'Receiving Port',icon:'🚛',type:'receiving',stock:250,capacity:500,moisture:18,temperature:24,impurities:.8,quality:87},
    {id:'holding',name:'Holding Bin',icon:'🛢️',type:'holding',stock:120,capacity:500,moisture:14.1,temperature:21,impurities:.5,quality:92},

    ...Array.from({length:7},(_,i)=>({
      id:`silo${i+1}`,
      name:`Silo ${i+1}`,
      icon:'🌾',
      type:'silo',
      stock:[720,540,880,310,630,470,790][i],
      capacity:1000,
      moisture:[13.5,13.8,12.8,14.2,13.2,13.7,13.1][i],
      temperature:[20.2,21.1,18.4,22.1,19.4,20.7,19.8][i],
      impurities:[.4,.5,.3,.6,.4,.5,.4][i],
      quality:[94,92,96,89,94,92,95][i]
    })),

    {id:'dryer',name:'Dryer',icon:'🔥',type:'dryer',stock:0,capacity:100,moisture:18,temperature:65,impurities:.5,quality:88},
    {id:'dispatch',name:'Dispatch Bin',icon:'📦',type:'dispatch',stock:0,capacity:400,moisture:13.2,temperature:20,impurities:.4,quality:94}
  ],

  equipment:[
    {name:'Receiving Conveyor',icon:'↗',status:'Running',speed:85,temp:35},
    {name:'Bucket Elevator 1',icon:'⇧',status:'Running',speed:85,temp:48},
    {name:'Transfer Conveyor A',icon:'⇢',status:'Running',speed:80,temp:34},
    {name:'Transfer Conveyor B',icon:'⇢',status:'Running',speed:80,temp:36},
    {name:'Dryer',icon:'♨',status:'Running',speed:75,temp:65},
    {name:'Furnace',icon:'♨',status:'Running',speed:70,temp:180},
    {name:'Aeration Fans',icon:'❋',status:'Running',speed:90,temp:28},
    {name:'Dispatch Conveyor',icon:'⇢',status:'Running',speed:80,temp:33},
    {name:'Loadout Gate',icon:'▤',status:'Standby',speed:0,temp:25}
  ]
};

const missions = [
 {
  id:'transfer',
  title:'Complete a safe grain transfer',
  description:'Move grain from the Holding Bin to Silo 1 without triggering a fault.',
  reward:100,
  check:()=>state.logs.some(l=>l.type==='TRANSFER'&&l.event.includes('Holding Bin')&&l.event.includes('Silo 1'))
 },
 {
  id:'inspect',
  title:'Inspect plant health',
  description:'Run the plant health check and review the operating recommendations.',
  reward:75,
  check:()=>state.logs.some(l=>l.event==='Plant health check completed')
 },
 {
  id:'quality',
  title:'Review grain quality',
  description:'Open Grain quality and apply the dryer setpoints.',
  reward:75,
  check:()=>state.logs.some(l=>l.event==='Dryer setpoints applied')
 },
 {
  id:'fault',
  title:'Practice fault recovery',
  description:'Inject a training fault, then clear it safely.',
  reward:150,
  check:()=>state.logs.some(l=>l.event==='Training fault cleared')
 },
 {
  id:'inventory',
  title:'Understand inventory',
  description:'Visit the Inventory page and inspect current stock levels.',
  reward:50,
  check:()=>state.logs.some(l=>l.event==='Inventory inspected')
 }
];

const locationById = id => state.nodes.find(n=>n.id===id);

function log(type,event,details='—'){
  state.logs.unshift({
    time:new Date().toLocaleTimeString(),
    type,
    event,
    details
  });

  if(state.logs.length>200) state.logs.length=200;

  renderLogs();
  renderAlarmCount();
}

function toast(message){
  const el=$('#toast');
  el.textContent=message;
  el.classList.add('show');

  clearTimeout(toast.timer);

  toast.timer=setTimeout(
    ()=>el.classList.remove('show'),
    3300
  );
}

function totalStock(){
  return state.nodes.reduce((sum,n)=>sum+n.stock,0);
}

function averageQuality(){
  const grain=state.nodes.filter(n=>n.stock>0);

  return grain.length
    ? Math.round(grain.reduce((s,n)=>s+n.quality,0)/grain.length)
    : 0;
}

function alarmCount(){
  return (state.emergencyStop?1:0)+(state.activeFault?1:0);
}

function healthPercent(){
  return state.equipment.filter(e=>e.status==='Running').length
    /state.equipment.length*100;
}

function navigate(view){
  $$('.view').forEach(v=>
    v.classList.toggle('active',v.id===`view-${view}`)
  );

  $$('.nav-item').forEach(b=>
    b.classList.toggle('active',b.dataset.view===view)
  );

  const titles={
    dashboard:'Plant dashboard',
    plant:'Plant map',
    inventory:'Grain inventory',
    quality:'Grain quality',
    equipment:'Equipment & faults',
    missions:'Learning missions',
    reports:'Event log'
  };

  $('#pageTitle').textContent=titles[view]||'Plant dashboard';
  $('#sidebar').classList.remove('open');

  if(view==='inventory'){
    log('SYSTEM','Inventory inspected','Current stock levels reviewed');
  }

  renderAll();
}

function getLocationOptions(){
  return state.nodes.map(n=>
    `<option value="${n.id}">${n.name}</option>`
  ).join('');
}

function initSelectors(){
  const opts=getLocationOptions();

  $('#sourceSelect').innerHTML=opts;
  $('#destinationSelect').innerHTML=opts;

  $('#sourceSelect').value='holding';
  $('#destinationSelect').value='silo1';
}

function renderNode(n,full=false){
  const fill=Math.min(100,n.stock/n.capacity*100);

  const faulted=state.activeFault&&(
    (state.activeFault==='Dryer overheat'&&n.id==='dryer')||
    (state.activeFault==='Level sensor failure'&&n.type==='silo')||
    (state.activeFault==='Gate mismatch'&&n.id==='dispatch')
  );

  const processing=n.id==='dryer'&&n.stock>0;

  return `
    <div class="${full?'map-node':'flow-node'} ${faulted?'alarm':''} ${processing?'processing':''}">
      <div class="node-icon">${n.icon}</div>

      ${full
        ? `<h3>${n.name}</h3>`
        : `<div class="node-name">${n.name}</div>`
      }

      ${full
        ? `<p>${n.type==='silo'?'Storage silo':n.type.charAt(0).toUpperCase()+n.type.slice(1)} · Capacity ${fmt(n.capacity)} t</p>
           <strong>${fmt(n.stock)} t</strong>
           <small> stored · ${Math.round(fill)}% full</small>`
        : `<div class="node-stock">${fmt(n.stock)} / ${fmt(n.capacity)} t</div>
           <span class="node-state">${faulted?'FAULT':processing?'PROCESSING':fill>90?'NEAR FULL':'AVAILABLE'}</span>`
      }
    </div>
  `;
}

function renderPlant(){
  const keyIds=[
    'receiving',
    'holding',
    'silo1',
    'silo2',
    'silo3',
    'silo4',
    'silo5',
    'silo6',
    'silo7',
    'dryer',
    'dispatch'
  ];

  const nodes=keyIds.map(id=>locationById(id));

  $('#plantFlow').innerHTML=nodes.map(n=>renderNode(n)).join('');
  $('#fullPlantMap').innerHTML=nodes.map(n=>renderNode(n,true)).join('');
}

function renderMetrics(){
  const total=totalStock();
  const quality=averageQuality();
  const health=Math.round(healthPercent());

  $('#totalStock').textContent=`${fmt(total)} t`;

  $('#qualityMetric').textContent=`${quality}/100`;
  $('#qualityBar').style.width=`${quality}%`;

  $('#healthMetric').textContent=`${health}%`;

  $('#healthCaption').textContent=
    state.activeFault
      ? 'fault detected — inspect equipment'
      : state.plantRunning
        ? 'equipment operating normally'
        : 'plant stopped';

  $('#healthBar').style.width=`${health}%`;

  $('#xpMetric').textContent=`${state.xp} XP`;
  $('#creditsMetric').textContent=state.credits;
  $('#xpBar').style.width=`${Math.min(100,state.xp/10)}%`;

  $('#receiptsMetric').textContent=`${fmt(state.receipts)} t`;
  $('#dispatchesMetric').textContent=`${fmt(state.dispatches)} t`;
  $('#lossesMetric').textContent=`${fmt(state.losses)} t`;

  $('#inventoryTotalTag').textContent=`${fmt(total)} tonnes total`;
}

function renderInventory(){
  const rows=state.nodes.map(n=>{
    const fill=Math.min(100,n.stock/n.capacity*100);

    return `
      <tr>
        <td>${n.icon} &nbsp;${n.name}</td>
        <td>${fmt(n.stock)} t</td>
        <td>${fmt(n.capacity)} t</td>
        <td>
          <div class="fill-cell">
            <div class="fill-track">
              <i style="width:${fill}%"></i>
            </div>
            <span>${Math.round(fill)}%</span>
          </div>
        </td>
        <td>${n.moisture.toFixed(1)}%</td>
        <td>${n.temperature.toFixed(1)}°C</td>
      </tr>
    `;
  }).join('');

  $('#inventoryRows').innerHTML=rows;
}

function qualityStatus(n){
  if(n.moisture>17||n.temperature>70||n.impurities>1){
    return ['Needs attention','bad'];
  }

  if(n.moisture>14.5||n.temperature>40||n.quality<90){
    return ['Monitor','warn'];
  }

  return ['Good',''];
}

function renderQuality(){
  const q=averageQuality();

  $('#qualityBig').innerHTML=`${q}<span>/100</span>`;
  $('#qualityBigBar').style.width=`${q}%`;

  $('#qualitySummary').textContent=
    q>=92
      ? 'Grain quality is within the training target.'
      : q>=85
        ? 'Some grain locations need monitoring. Review moisture and temperature.'
        : 'Quality is below the target. Inspect the grain and drying process.';

  $('#dryerTempValue').textContent=state.dryerSetpoint;
  $('#furnaceTempValue').textContent=state.furnaceSetpoint;

  $('#dryerTempRange').value=state.dryerSetpoint;
  $('#furnaceTempRange').value=state.furnaceSetpoint;

  $('#qualityRows').innerHTML=state.nodes
    .filter(n=>n.stock>0)
    .map(n=>{
      const [status,cls]=qualityStatus(n);

      return `
        <tr>
          <td>${n.name}</td>
          <td>${n.moisture.toFixed(1)}%</td>
          <td>${n.temperature.toFixed(1)}°C</td>
          <td>${n.impurities.toFixed(1)}%</td>
          <td><strong>${n.quality}/100</strong></td>
          <td><span class="status-pill ${cls}">${status}</span></td>
        </tr>
      `;
    }).join('');
}

function renderEquipment(){
  const icons=['↗','⇧','⇢','⇢','♨','♨','❋','⇢','▤'];

  $('#equipmentGrid').innerHTML=state.equipment.map((e,i)=>{
    const stopped=e.status==='Stopped';

    const warning=state.activeFault&&(
      (state.activeFault==='Conveyor blockage'&&e.name.includes('Conveyor'))||
      (state.activeFault==='Belt slip'&&e.name.includes('Conveyor'))||
      (state.activeFault==='Elevator bearing warning'&&e.name.includes('Elevator'))||
      (state.activeFault==='Dryer overheat'&&['Dryer','Furnace'].includes(e.name))||
      (state.activeFault==='Gate mismatch'&&e.name==='Loadout Gate')||
      (state.activeFault==='Level sensor failure'&&i===6)
    );

    return `
      <article class="equipment-card">
        <div class="equipment-head">
          <div class="equipment-icon">${e.icon||icons[i]}</div>

          <div>
            <h3>${e.name}</h3>
            <small>${warning?'Training fault detected':'Plant equipment'}</small>
          </div>

          <span class="status-pill ${warning||stopped?'bad':e.status==='Standby'?'warn':''}">
            ${warning?'Fault':e.status}
          </span>
        </div>

        <div class="equipment-stats">
          <span>Speed <strong>${e.speed}%</strong></span>
          <span>Temp <strong>${e.temp}°C</strong></span>
        </div>
      </article>
    `;
  }).join('');
}
function renderMissions(){
  missions.forEach(m=>{
    if(!state.missionCompleted.has(m.id)&&m.check()){
      state.missionCompleted.add(m.id);
      state.xp+=m.reward;
      state.credits+=Math.round(m.reward/5);

      log('SYSTEM',`Mission completed: ${m.title}`,`+${m.reward} XP`);

      toast(`Mission completed: ${m.title} (+${m.reward} XP)`);
    }
  });

  $('#missionsGrid').innerHTML=missions.map((m,i)=>{
    const done=state.missionCompleted.has(m.id);

    return `
      <article class="mission-card ${done?'completed':''}">
        <span class="mission-number">MISSION 0${i+1}</span>
        <h3>${m.title}</h3>
        <p>${m.description}</p>
        <span class="mission-reward">✦ Reward: ${m.reward} XP</span>

        <button
          class="${done?'soft-button':'primary-button'}"
          ${done?'disabled':''}
          data-mission="${m.id}">
          ${done?'✓ Completed':'Check progress'}
        </button>
      </article>
    `;
  }).join('');

  $('#missionXpTag').textContent=`${state.xp} XP earned`;

  $$('[data-mission]').forEach(b=>{
    b.addEventListener('click',()=>{
      const m=missions.find(x=>x.id===b.dataset.mission);

      toast(
        m.check()
          ? 'Mission condition met. Refreshing progress.'
          : 'Not complete yet. Follow the mission instructions, then check again.'
      );

      renderMissions();
      renderMetrics();
    });
  });
}

function renderLogs(){
  if(!$('#logRows')) return;

  $('#logRows').innerHTML=state.logs.length
    ? state.logs.map(l=>`
        <tr>
          <td>${l.time}</td>
          <td><span class="log-type ${l.type}">${l.type}</span></td>
          <td>${l.event}</td>
          <td>${l.details}</td>
        </tr>
      `).join('')
    : '<tr><td colspan="4">No events recorded yet.</td></tr>';
}

function renderAlarmCount(){
  $('#alarmCount').textContent=alarmCount();
  $('#safetyBanner').hidden=!state.emergencyStop;
}

function renderAll(){
  renderPlant();
  renderMetrics();
  renderInventory();
  renderQuality();
  renderEquipment();
  renderMissions();
  renderLogs();
  renderAlarmCount();
  updateAssistant();
}

function updateAssistant(){
  let message;

  if(state.emergencyStop){
    message='Emergency stop is active. Keep transfers stopped, identify the cause, and reset only after confirming the simulated plant is safe.';
  }else if(state.activeFault){
    message=`Active training fault: ${state.activeFault}. Stop affected equipment, inspect the route and clear the fault only after the simulated issue is addressed.`;
  }else if(!state.plantRunning){
    message='The plant is stopped. Inspect the equipment list and confirm routes are available before restarting the simulation.';
  }else{
    const nearFull=state.nodes.find(n=>n.stock/n.capacity>.9);
    const wet=state.nodes.find(n=>n.stock>0&&n.moisture>17);

    if(nearFull){
      message=`${nearFull.name} is above 90% capacity. Avoid sending more grain there until sufficient headroom is available.`;
    }else if(wet){
      message=`${wet.name} has elevated moisture (${wet.moisture.toFixed(1)}%). Review drying needs before dispatch.`;
    }else{
      message='Plant status looks stable. Verify the selected route, available stock and destination headroom before each transfer.';
    }
  }

  $('#assistantMessage').textContent=message;
}

function routeAllowed(src,dst){
  if(src.id===dst.id){
    return [false,'Choose two different locations.'];
  }

  if(src.stock<=0){
    return [false,`${src.name} has no grain available.`];
  }

  if(dst.capacity-dst.stock<=0){
    return [false,`${dst.name} has no available capacity.`];
  }

  if(state.emergencyStop){
    return [false,'Emergency stop is active. Reset safety before transferring.'];
  }

  if(!state.plantRunning){
    return [false,'Plant is stopped. Start the plant before transferring.'];
  }

  if(state.activeFault){
    return [false,`Transfer blocked by active fault: ${state.activeFault}.`];
  }

  if(src.id==='dispatch'&&dst.id==='receiving'){
    return [false,'This training route is not configured.'];
  }

  if(dst.id==='dryer'&&state.dryerSetpoint>80){
    return [false,'Dryer setpoint is too high for this training scenario.'];
  }

  return [true,'Route available.'];
}

function transfer(sourceId,destinationId,amount){
  const src=locationById(sourceId);
  const dst=locationById(destinationId);

  if(!src||!dst) return;

  amount=Number(amount);

  if(!Number.isFinite(amount)||amount<=0){
    toast('Enter a transfer quantity greater than zero.');
    return;
  }

  const [allowed,reason]=routeAllowed(src,dst);

  if(!allowed){
    log('ALARM','Transfer rejected',reason);
    toast(reason);
    renderAll();
    return;
  }

  if(amount>src.stock){
    toast(`Only ${fmt(src.stock)} tonnes are available in ${src.name}.`);
    return;
  }

  if(amount>dst.capacity-dst.stock){
    toast(`${dst.name} has room for only ${fmt(dst.capacity-dst.stock)} tonnes.`);
    return;
  }

  const previousDestinationStock=dst.stock;

  src.stock=Math.round((src.stock-amount)*10)/10;
  dst.stock=Math.round((dst.stock+amount)*10)/10;

  dst.moisture=(
    dst.moisture*previousDestinationStock+
    src.moisture*amount
  )/dst.stock;

  dst.impurities=(
    dst.impurities*previousDestinationStock+
    src.impurities*amount
  )/dst.stock;

  dst.quality=Math.round(
    (
      dst.quality*previousDestinationStock+
      src.quality*amount
    )/dst.stock
  );

  if(dst.id==='dispatch'){
    state.dispatches+=amount;
  }

  if(src.id==='receiving'){
    state.receipts+=amount;
  }

  if(dst.id==='dryer'){
    dst.temperature=state.dryerSetpoint;
    dst.moisture=Math.max(10,dst.moisture-1.5);
  }

  state.xp+=10;
  state.credits=Math.max(0,state.credits-1);

  log(
    'TRANSFER',
    `${src.name} → ${dst.name}`,
    `${fmt(amount)} tonnes moved successfully`
  );

  toast(`Transfer complete: ${fmt(amount)} tonnes moved to ${dst.name}.`);

  renderAll();
}

function plantCheck(){
  const issues=[];

  if(state.activeFault){
    issues.push(`active fault: ${state.activeFault}`);
  }

  if(state.emergencyStop){
    issues.push('emergency stop is active');
  }

  if(!state.plantRunning){
    issues.push('plant is stopped');
  }

  const near=state.nodes.filter(n=>n.stock/n.capacity>.9);

  if(near.length){
    issues.push(`${near.map(n=>n.name).join(', ')} near capacity`);
  }

  const wet=state.nodes.filter(n=>n.stock>0&&n.moisture>17);

  if(wet.length){
    issues.push(`${wet.map(n=>n.name).join(', ')} moisture is high`);
  }

  const message=issues.length
    ? `Plant check found ${issues.join('; ')}. Review these items before operating.`
    : 'Plant check complete: no active faults, emergency stop is clear and storage headroom is available.';

  $('#assistantMessage').textContent=message;

  log(
    'SYSTEM',
    'Plant health check completed',
    issues.length?issues.join('; '):'No immediate simulated issues found'
  );

  toast('Plant check completed. See assistant guidance.');

  renderAll();
}

function injectFault(){
  if(state.activeFault){
    toast(`Clear the current fault (${state.activeFault}) before injecting another.`);
    return;
  }

  state.activeFault=$('#faultSelect').value;
  state.plantRunning=false;

  const affected=state.equipment.find(e=>
    (state.activeFault.includes('Conveyor')&&e.name.includes('Conveyor'))||
    (state.activeFault==='Belt slip'&&e.name.includes('Conveyor'))||
    (state.activeFault.includes('Elevator')&&e.name.includes('Elevator'))||
    (state.activeFault.includes('Dryer')&&e.name==='Dryer')||
    (state.activeFault.includes('Gate')&&e.name==='Loadout Gate')
  );

  if(affected){
    affected.status='Stopped';
  }

  log('ALARM','Training fault injected',state.activeFault);

  toast(`Training fault injected: ${state.activeFault}. Plant stopped for safety.`);

  renderAll();
}

function clearFault(){
  if(!state.activeFault){
    toast('There is no active fault to clear.');
    return;
  }

  const old=state.activeFault;

  state.activeFault=null;

  state.equipment.forEach(e=>{
    if(e.name==='Loadout Gate'){
      e.status='Standby';
    }else{
      e.status='Running';
    }
  });

  log('SYSTEM','Training fault cleared',`${old} cleared by operator`);

  toast('Fault cleared. Inspect the plant before restarting.');

  renderAll();
}

function emergencyStop(){
  state.emergencyStop=true;
  state.plantRunning=false;

  state.equipment.forEach(e=>{
    if(e.name!=='Loadout Gate'){
      e.status='Stopped';
    }
  });

  log('ALARM','Emergency stop activated','All simulated transfers blocked');

  renderAll();

  toast('Emergency stop active. All transfers are blocked.');
}

function resetEmergencyStop(){
  if(state.activeFault){
    toast('Clear the active fault before resetting the emergency stop.');
    return;
  }
function startPlant(){
  if(state.emergencyStop||state.activeFault){
    toast('Cannot start: clear the active fault and reset the emergency stop first.');
    return;
  }

  state.plantRunning=true;

  state.equipment.forEach(e=>{
    if(e.name!=='Loadout Gate'){
      e.status='Running';
    }
  });

  log('SYSTEM','Plant started','Operator started the simulated plant');

  renderAll();

  toast('Plant started in simulation.');
}

function stopPlant(){
  state.plantRunning=false;

  state.equipment.forEach(e=>{
    if(e.name!=='Loadout Gate'){
      e.status='Stopped';
    }
  });

  log('SYSTEM','Plant stopped','Operator stopped the simulated plant');

  renderAll();

  toast('Plant stopped. Transfers are disabled.');
}

function exportCSV(){
  const rows=[
    ['Time','Type','Event','Details'],
    ...state.logs.map(l=>[l.time,l.type,l.event,l.details])
  ];

  const csv=rows.map(row=>
    row.map(v=>`"${String(v).replaceAll('"','""')}"`).join(',')
  ).join('\r\n');

  const blob=new Blob(
    [csv],
    {type:'text/csv;charset=utf-8;'}
  );

  const url=URL.createObjectURL(blob);

  const a=document.createElement('a');
  a.href=url;
  a.download='grainwise-event-log.csv';
  a.click();

  URL.revokeObjectURL(url);

  toast('Event log exported as CSV.');
}

function updateClock(){
  const now=new Date();

  $('#clock').textContent=now.toLocaleTimeString();

  $('#dateChip').textContent=now.toLocaleDateString(
    undefined,
    {
      weekday:'short',
      month:'short',
      day:'numeric'
    }
  ).toUpperCase();
}

$$('.nav-item').forEach(b=>
  b.addEventListener('click',()=>navigate(b.dataset.view))
);

$$('[data-goto]').forEach(b=>
  b.addEventListener('click',()=>navigate(b.dataset.goto))
);

$('#menuToggle').addEventListener('click',()=>
  $('#sidebar').classList.toggle('open')
);

$('#transferForm').addEventListener('submit',e=>{
  e.preventDefault();

  transfer(
    $('#sourceSelect').value,
    $('#destinationSelect').value,
    $('#amountInput').value
  );
});

$('#plantTransferMount').innerHTML=$('#transferForm').outerHTML;

$('#plantTransferMount form').id='plantTransferForm';

$('#plantTransferForm').addEventListener('submit',e=>{
  e.preventDefault();

  transfer(
    $('#plantTransferForm #sourceSelect').value,
    $('#plantTransferForm #destinationSelect').value,
    $('#plantTransferForm #amountInput').value
  );
});

$('#checkPlantButton').addEventListener('click',plantCheck);

$('#estopButton').addEventListener('click',emergencyStop);

$('#alarmButton').addEventListener('click',()=>navigate('reports'));

$('#injectFaultButton').addEventListener('click',injectFault);

$('#clearFaultsButton').addEventListener('click',clearFault);

$('#plantStartButton').addEventListener('click',startPlant);

$('#plantStopButton').addEventListener('click',stopPlant);

$('#resetSafetyButton').addEventListener('click',resetEmergencyStop);

$('#dryerTempRange').addEventListener('input',e=>
  $('#dryerTempValue').textContent=e.target.value
);

$('#furnaceTempRange').addEventListener('input',e=>
  $('#furnaceTempValue').textContent=e.target.value
);

$('#applyDryerButton').addEventListener('click',()=>{
  state.dryerSetpoint=Number($('#dryerTempRange').value);
  state.furnaceSetpoint=Number($('#furnaceTempRange').value);

  const dryer=locationById('dryer');

  dryer.temperature=state.dryerSetpoint;

  state.equipment.find(e=>e.name==='Dryer').temp=state.dryerSetpoint;

  state.equipment.find(e=>e.name==='Furnace').temp=state.furnaceSetpoint;

  log(
    'SYSTEM',
    'Dryer setpoints applied',
    `Dryer ${state.dryerSetpoint}°C; furnace ${state.furnaceSetpoint}°C`
  );

  toast('Dryer and furnace setpoints applied to the simulation.');

  renderAll();
});

$('#exportLogButton').addEventListener('click',exportCSV);

initSelectors();

updateClock();

setInterval(updateClock,1000);

log(
  'SYSTEM',
  'Simulation initialized',
  'GrainWise browser prototype ready'
);

renderAll();
  state.emergencyStop=false;
  state.plantRunning=true;

  state.equipment.forEach(e=>{
    if(e.name!=='Loadout Gate'){
      e.status='Running';
    }
  });

  log('SYSTEM','Emergency stop reset','Operator reset after simulated safety check');

  renderAll();

  toast('Emergency stop reset. Plant running in simulation.');
}

